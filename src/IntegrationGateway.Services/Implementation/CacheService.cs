using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IntegrationGateway.Services.Configuration;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Services.Implementation;

public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheService> _logger;
    private readonly CacheOptions _options;
    private readonly ConcurrentDictionary<string, bool> _keyRegistry = new();

    public CacheService(IMemoryCache cache, ILogger<CacheService> logger, IOptions<CacheOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            if (_cache.TryGetValue(key, out var cachedValue))
            {
                if (cachedValue is T directValue)
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return Task.FromResult<T?>(directValue);
                }
                
                if (cachedValue is string jsonValue)
                {
                    var deserializedValue = JsonSerializer.Deserialize<T>(jsonValue);
                    _logger.LogDebug("Cache hit (deserialized) for key: {Key}", key);
                    return Task.FromResult(deserializedValue);
                }
            }
            
            _logger.LogDebug("Cache miss for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var cacheExpiration = expiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes);
            
            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheExpiration,
                SlidingExpiration = TimeSpan.FromMinutes(Math.Min(cacheExpiration.TotalMinutes / 2, 5)),
                Priority = CacheItemPriority.Normal
            };
            
            entryOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                _keyRegistry.TryRemove(key.ToString() ?? string.Empty, out _);
                _logger.LogDebug("Cache entry evicted: {Key}, Reason: {Reason}", key, reason);
            });

            _cache.Set(key, value, entryOptions);
            _keyRegistry.TryAdd(key, true);
            
            _logger.LogDebug("Cache set for key: {Key}, Expiration: {Expiration}", key, cacheExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache for key: {Key}", key);
        }
        
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            _cache.Remove(key);
            _keyRegistry.TryRemove(key, out _);
            _logger.LogDebug("Cache removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing from cache for key: {Key}", key);
        }
        
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var keysToRemove = _keyRegistry.Keys
                .Where(key => key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
                _keyRegistry.TryRemove(key, out _);
            }
            
            _logger.LogDebug("Cache cleared for pattern: {Pattern}, Keys removed: {Count}", pattern, keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing from cache by pattern: {Pattern}", pattern);
        }
        
        return Task.CompletedTask;
    }
}