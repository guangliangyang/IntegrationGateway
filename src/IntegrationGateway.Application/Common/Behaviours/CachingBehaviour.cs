using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behavior for automatic caching of query results
/// 
/// DEMO IMPLEMENTATION NOTES:
/// - Uses in-memory cache (IMemoryCache) with TTL-based expiration only
/// - Cache invalidation logic removed due to in-memory cache limitations
/// - Default 5-second TTL balances performance and data freshness
/// 
/// PRODUCTION CONSIDERATIONS:
/// - Replace with Redis distributed cache for scalability
/// - Implement event-driven cache invalidation for immediate consistency
/// - Add cache metrics and monitoring capabilities
/// - Consider cache partitioning strategies
/// </summary>
public class CachingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CachingBehaviour<TRequest, TResponse>> _logger;

    public CachingBehaviour(IMemoryCache cache, IConfiguration configuration, ILogger<CachingBehaviour<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var cacheAttribute = typeof(TRequest).GetCustomAttribute<CacheableAttribute>();
        
        if (cacheAttribute == null)
        {
            return await next();
        }

        var cacheKey = GenerateCacheKey(request, cacheAttribute);
        
        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out TResponse? cachedResponse))
        {
            _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
            return cachedResponse!;
        }

        _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);

        // Execute the request
        var response = await next();

        // Get cache duration from configuration or attribute
        var cacheDurationSeconds = GetCacheDuration(request, cacheAttribute);

        // Cache the response
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheDurationSeconds),
            Priority = CacheItemPriority.Normal
        };

        _cache.Set(cacheKey, response, cacheEntryOptions);
        
        _logger.LogDebug("Cached response for key: {CacheKey}, Duration: {DurationSeconds}s", 
            cacheKey, cacheDurationSeconds);

        return response;
    }

    private int GetCacheDuration(TRequest request, CacheableAttribute cacheAttribute)
    {
        var requestName = typeof(TRequest).Name;
        
        // Try to get specific cache duration from configuration
        var configKey = $"Cache:{requestName}ExpirationSeconds";
        var configSeconds = _configuration.GetValue<int?>(configKey);
        if (configSeconds.HasValue)
        {
            return configSeconds.Value;
        }

        // Fallback to default cache duration from configuration
        var defaultSeconds = _configuration.GetValue<int?>("Cache:DefaultExpirationSeconds");
        if (defaultSeconds.HasValue)
        {
            return defaultSeconds.Value;
        }

        // Final fallback to attribute value
        return cacheAttribute.DurationSeconds;
    }

    private static string GenerateCacheKey(TRequest request, CacheableAttribute cacheAttribute)
    {
        var requestName = typeof(TRequest).Name;
        
        if (!string.IsNullOrEmpty(cacheAttribute.CustomKeyPattern))
        {
            return $"{requestName}_{cacheAttribute.CustomKeyPattern}";
        }

        // Generate cache key based on request properties
        var requestJson = JsonSerializer.Serialize(request);
        var requestHash = requestJson.GetHashCode();
        
        return $"{requestName}_{requestHash:X}";
    }
}

/// <summary>
/// Attribute to mark requests as cacheable
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class CacheableAttribute : Attribute
{
    public CacheableAttribute(int durationSeconds)
    {
        DurationSeconds = durationSeconds;
    }

    /// <summary>
    /// Cache duration in seconds
    /// </summary>
    public int DurationSeconds { get; }

    /// <summary>
    /// Custom pattern for generating cache keys
    /// </summary>
    public string? CustomKeyPattern { get; set; }
}

// NOTE: Cache invalidation interfaces and behaviors removed
// REASON: In-memory cache limitations with pattern matching and distributed environment issues
// SOLUTION: Using TTL-based expiration (5 seconds) for demo purposes
// 
// For production environments, consider:
// - Redis with SCAN + DEL for pattern-based invalidation
// - Event-driven cache invalidation using domain events
// - Distributed cache invalidation strategies