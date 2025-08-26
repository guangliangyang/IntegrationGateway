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
    
    public int DefaultExpirationMinutes { get; set; } = 5;
    
    public int ProductListExpirationMinutes { get; set; } = 2;
    
    public int ProductDetailExpirationMinutes { get; set; } = 10;
}

public class CircuitBreakerOptions
{
    public const string SectionName = "CircuitBreaker";
    
    public int FailureThreshold { get; set; } = 5;
    
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromMinutes(1);
    
    public int SamplingDuration { get; set; } = 10;
}