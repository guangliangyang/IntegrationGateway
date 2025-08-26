using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using IntegrationGateway.Models.Common;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Services.Implementation;

public class IdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, IdempotencyKey> _store = new();
    private readonly ILogger<IdempotencyService> _logger;
    private readonly Timer _cleanupTimer;

    public IdempotencyService(ILogger<IdempotencyService> logger)
    {
        _logger = logger;
        
        // Cleanup expired entries every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public Task<IdempotencyKey?> GetAsync(string key, string operation, string bodyHash, CancellationToken cancellationToken = default)
    {
        var compositeKey = GenerateCompositeKey(key, operation, bodyHash);
        
        if (_store.TryGetValue(compositeKey, out var idempotencyKey))
        {
            if (!idempotencyKey.IsExpired)
            {
                _logger.LogDebug("Idempotency key found: {Key}", compositeKey);
                return Task.FromResult<IdempotencyKey?>(idempotencyKey);
            }
            
            // Remove expired entry
            _store.TryRemove(compositeKey, out _);
            _logger.LogDebug("Expired idempotency key removed: {Key}", compositeKey);
        }
        
        return Task.FromResult<IdempotencyKey?>(null);
    }

    public Task SetAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default)
    {
        var compositeKey = idempotencyKey.GetCompositeKey();
        
        _store.AddOrUpdate(
            compositeKey,
            idempotencyKey,
            (key, existing) => idempotencyKey);
        
        _logger.LogDebug("Idempotency key stored: {Key}", compositeKey);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, string operation, string bodyHash, CancellationToken cancellationToken = default)
    {
        var compositeKey = GenerateCompositeKey(key, operation, bodyHash);
        
        if (_store.TryGetValue(compositeKey, out var idempotencyKey))
        {
            if (!idempotencyKey.IsExpired)
            {
                return Task.FromResult(true);
            }
            
            // Remove expired entry
            _store.TryRemove(compositeKey, out _);
        }
        
        return Task.FromResult(false);
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
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired idempotency keys", expiredKeys.Count);
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
    }
}