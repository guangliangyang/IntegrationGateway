using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behavior for automatic caching of query results
/// </summary>
public class CachingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingBehaviour<TRequest, TResponse>> _logger;

    public CachingBehaviour(IMemoryCache cache, ILogger<CachingBehaviour<TRequest, TResponse>> logger)
    {
        _cache = cache;
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

        // Cache the response
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheAttribute.DurationSeconds),
            Priority = CacheItemPriority.Normal
        };

        _cache.Set(cacheKey, response, cacheEntryOptions);
        
        _logger.LogDebug("Cached response for key: {CacheKey}, Duration: {DurationSeconds}s", 
            cacheKey, cacheAttribute.DurationSeconds);

        return response;
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

/// <summary>
/// Interface for requests that can invalidate cache entries
/// </summary>
public interface ICacheInvalidating
{
    /// <summary>
    /// Get cache keys that should be invalidated after this operation
    /// </summary>
    IEnumerable<string> GetCacheKeysToInvalidate();
}

/// <summary>
/// Cache invalidation behavior for commands that modify data
/// </summary>
public class CacheInvalidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheInvalidationBehaviour<TRequest, TResponse>> _logger;

    public CacheInvalidationBehaviour(IMemoryCache cache, ILogger<CacheInvalidationBehaviour<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is ICacheInvalidating cacheInvalidating)
        {
            var keysToInvalidate = cacheInvalidating.GetCacheKeysToInvalidate();
            
            foreach (var key in keysToInvalidate)
            {
                _cache.Remove(key);
                _logger.LogDebug("Invalidated cache key: {CacheKey}", key);
            }
        }

        return response;
    }
}