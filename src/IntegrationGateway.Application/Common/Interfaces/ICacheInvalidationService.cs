using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Common.Behaviours;

public interface ICacheInvalidationService
{
    Task InvalidateCacheKeysAsync(IEnumerable<string> cacheKeys, CancellationToken cancellationToken = default);
    Task InvalidateCachePatternAsync(string pattern, CancellationToken cancellationToken = default);
}

public class CacheInvalidationService : ICacheInvalidationService
{
    private readonly ILogger<CacheInvalidationService> _logger;

    public CacheInvalidationService(ILogger<CacheInvalidationService> logger)
    {
        _logger = logger;
    }

    public async Task InvalidateCacheKeysAsync(IEnumerable<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        foreach (var key in cacheKeys)
        {
            if (key.EndsWith("*"))
            {
                await InvalidateCachePatternAsync(key, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Invalidating cache key: {CacheKey}", key);
                // TODO: Implement actual cache invalidation
                // This would typically call your distributed cache service
            }
        }
    }

    public async Task InvalidateCachePatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating cache pattern: {CachePattern}", pattern);
        
        // TODO: Implement pattern-based cache invalidation
        // This would typically scan cache keys matching the pattern and remove them
        // For Redis: use SCAN command with pattern matching
        // For in-memory cache: iterate through keys
        
        await Task.CompletedTask;
    }
}