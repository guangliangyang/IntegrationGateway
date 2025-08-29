namespace IntegrationGateway.Api.Configuration.Middleware;

public class IdempotencyOptions
{
    public const string SectionName = "Idempotency";
    
    /// <summary>
    /// Whether idempotency middleware is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Default expiration time for idempotency operations
    /// </summary>
    public TimeSpan DefaultExpirationTime { get; set; } = TimeSpan.FromHours(1);
    
    /// <summary>
    /// Maximum time to wait for a semaphore lock
    /// </summary>
    public TimeSpan SemaphoreTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Interval for cleanup expired idempotency entries
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(15);
    
    /// <summary>
    /// Maximum number of concurrent operations allowed
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = 1000;
    
    /// <summary>
    /// Enable cleanup of expired entries
    /// </summary>
    public bool EnableCleanup { get; set; } = true;
    
    /// <summary>
    /// Required header name for idempotency key
    /// </summary>
    public string HeaderName { get; set; } = "Idempotency-Key";
    
    /// <summary>
    /// Minimum length for idempotency key
    /// </summary>
    public int MinKeyLength { get; set; } = 8;
    
    /// <summary>
    /// Maximum length for idempotency key
    /// </summary>
    public int MaxKeyLength { get; set; } = 128;
}