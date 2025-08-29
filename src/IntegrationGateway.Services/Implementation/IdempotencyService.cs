using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using IntegrationGateway.Models.Common;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Services.Implementation;

public class IdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, IdempotencyKey> _store = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ILogger<IdempotencyService> _logger;
    private readonly Timer _cleanupTimer;

    public IdempotencyService(ILogger<IdempotencyService> logger)
    {
        _logger = logger;
        
        // Cleanup expired entries every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task<IdempotencyKey?> GetAsync(string key, string operation, string bodyHash, CancellationToken cancellationToken = default)
    {
        var compositeKey = GenerateCompositeKey(key, operation, bodyHash);
        
        if (_store.TryGetValue(compositeKey, out var idempotencyKey))
        {
            if (!idempotencyKey.IsExpired)
            {
                _logger.LogDebug("Idempotency key found: {Key}", compositeKey);
                return idempotencyKey;
            }
            
            // Remove expired entry
            _store.TryRemove(compositeKey, out _);
            _logger.LogDebug("Expired idempotency key removed: {Key}", compositeKey);
        }
        
        return null;
    }

    public async Task SetAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default)
    {
        var compositeKey = idempotencyKey.GetCompositeKey();
        
        _store.AddOrUpdate(
            compositeKey,
            idempotencyKey,
            (key, existing) => idempotencyKey);
        
        _logger.LogDebug("Idempotency key stored: {Key}", compositeKey);
    }

    public async Task<bool> ExistsAsync(string key, string operation, string bodyHash, CancellationToken cancellationToken = default)
    {
        var compositeKey = GenerateCompositeKey(key, operation, bodyHash);
        
        if (_store.TryGetValue(compositeKey, out var idempotencyKey))
        {
            if (!idempotencyKey.IsExpired)
            {
                return true;
            }
            
            // Remove expired entry
            _store.TryRemove(compositeKey, out _);
        }
        
        return false;
    }

    /// <summary>
    /// High-concurrency safe method to get or create idempotency operation with locking
    /// </summary>
    public async Task<(bool IsExisting, IdempotencyKey Operation)> GetOrCreateOperationAsync(
        string key, string operation, string bodyHash, CancellationToken cancellationToken = default)
    {
        var compositeKey = GenerateCompositeKey(key, operation, bodyHash);
        
        // Get or create a semaphore for this specific operation
        var semaphore = _locks.GetOrAdd(compositeKey, _ => new SemaphoreSlim(1, 1));
        
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check pattern: check again after acquiring lock
            if (_store.TryGetValue(compositeKey, out var existingOperation))
            {
                if (!existingOperation.IsExpired)
                {
                    _logger.LogDebug("Found existing idempotent operation: {Key}", compositeKey);
                    return (IsExisting: true, Operation: existingOperation);
                }
                
                // Remove expired entry
                _store.TryRemove(compositeKey, out _);
                _logger.LogDebug("Removed expired idempotent operation: {Key}", compositeKey);
            }
            
            // Create new operation marker (without response initially)
            var newOperation = new IdempotencyKey
            {
                Key = key,
                Operation = operation,
                BodyHash = bodyHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresIn = TimeSpan.FromHours(24)
                // ResponseBody and ResponseStatusCode will be set later
            };
            
            _store.TryAdd(compositeKey, newOperation);
            _logger.LogDebug("Created new idempotent operation: {Key}", compositeKey);
            
            return (IsExisting: false, Operation: newOperation);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Update operation with response data (called after business logic completes)
    /// </summary>
    public async Task UpdateOperationResponseAsync(
        string key, string operation, string bodyHash, string responseBody, int statusCode, 
        CancellationToken cancellationToken = default)
    {
        var compositeKey = GenerateCompositeKey(key, operation, bodyHash);
        
        if (_store.TryGetValue(compositeKey, out var existingOperation))
        {
            existingOperation.ResponseBody = responseBody;
            existingOperation.ResponseStatusCode = statusCode;
            
            _logger.LogDebug("Updated idempotent operation response: {Key}, Status: {Status}", 
                compositeKey, statusCode);
        }
        else
        {
            _logger.LogWarning("Attempted to update non-existent idempotent operation: {Key}", compositeKey);
        }
    }

    public string GenerateCompositeKey(string key, string operation, string bodyHash)
    {
        return $"{key}|{operation}|{bodyHash}";
    }

    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            var expiredKeys = _store
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _store.TryRemove(key, out _);
                
                // Also cleanup associated semaphores to prevent memory leaks
                if (_locks.TryRemove(key, out var semaphore))
                {
                    semaphore?.Dispose();
                }
            }

            // Additional cleanup: remove locks for keys that no longer exist in store
            // This handles cases where locks might accumulate without corresponding store entries
            var orphanedLockKeys = _locks.Keys.Except(_store.Keys).ToList();
            foreach (var orphanedKey in orphanedLockKeys)
            {
                if (_locks.TryRemove(orphanedKey, out var orphanedSemaphore))
                {
                    orphanedSemaphore?.Dispose();
                }
            }

            var totalCleaned = expiredKeys.Count + orphanedLockKeys.Count;
            if (totalCleaned > 0)
            {
                _logger.LogInformation("Cleaned up {ExpiredCount} expired idempotency keys and {OrphanedCount} orphaned locks", 
                    expiredKeys.Count, orphanedLockKeys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during idempotency cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        
        // Dispose all semaphores
        foreach (var semaphore in _locks.Values)
        {
            semaphore?.Dispose();
        }
        _locks.Clear();
    }
}