namespace IntegrationGateway.Services.Configuration;

public class ErpServiceOptions
{
    public const string SectionName = "ErpService";
    
    public string BaseUrl { get; set; } = string.Empty;
    
    public int TimeoutSeconds { get; set; } = 30;
    
    public int MaxRetries { get; set; } = 3;
    
    public string? ApiKey { get; set; }
}

public class WarehouseServiceOptions
{
    public const string SectionName = "WarehouseService";
    
    public string BaseUrl { get; set; } = string.Empty;
    
    public int TimeoutSeconds { get; set; } = 30;
    
    public int MaxRetries { get; set; } = 3;
    
    public string? ApiKey { get; set; }
}

public class CacheOptions
{
    public const string SectionName = "Cache";
    
    /// <summary>
    /// Whether caching is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    public int DefaultExpirationMinutes { get; set; } = 5;
    
    public int ProductListExpirationMinutes { get; set; } = 2;
    
    public int ProductDetailExpirationMinutes { get; set; } = 10;
}

public class CircuitBreakerOptions
{
    public const string SectionName = "CircuitBreaker";
    
    /// <summary>
    /// Whether circuit breaker is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Number of consecutive failures before opening the circuit breaker
    /// </summary>
    public int FailureThreshold { get; set; } = 5;
    
    /// <summary>
    /// Duration to keep the circuit breaker open after failure threshold is reached
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// Sampling duration window in seconds
    /// </summary>
    public int SamplingDuration { get; set; } = 10;
    
    /// <summary>
    /// Minimum number of requests in sampling period before failures are evaluated
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;
}