using System.ComponentModel.DataAnnotations;

namespace IntegrationGateway.Api.Configuration;

/// <summary>
/// Configuration options for security controls
/// </summary>
public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// CORS configuration
    /// </summary>
    public CorsOptions Cors { get; set; } = new();

    /// <summary>
    /// Rate limiting configuration
    /// </summary>
    public RateLimitingOptions RateLimiting { get; set; } = new();

    /// <summary>
    /// Request size limits configuration
    /// </summary>
    public RequestLimitsOptions RequestLimits { get; set; } = new();

    /// <summary>
    /// SSRF protection configuration
    /// </summary>
    public SsrfProtectionOptions SsrfProtection { get; set; } = new();
}

/// <summary>
/// CORS configuration options
/// </summary>
public class CorsOptions
{
    /// <summary>
    /// Allowed origins for CORS
    /// </summary>
    [Required]
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether to allow credentials in CORS requests
    /// </summary>
    public bool AllowCredentials { get; set; } = false;

    /// <summary>
    /// Maximum age for preflight request caching in seconds
    /// </summary>
    public int PreflightMaxAge { get; set; } = 86400; // 24 hours
}

/// <summary>
/// Rate limiting configuration options
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Whether rate limiting is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// General API rate limit per IP
    /// </summary>
    public RateLimitPolicy GeneralApi { get; set; } = new()
    {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 10
    };

    /// <summary>
    /// Authentication endpoint rate limit per IP
    /// </summary>
    public RateLimitPolicy Authentication { get; set; } = new()
    {
        PermitLimit = 5,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 2
    };

    /// <summary>
    /// Write operations rate limit per user
    /// </summary>
    public RateLimitPolicy WriteOperations { get; set; } = new()
    {
        PermitLimit = 20,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 5
    };
}

/// <summary>
/// Individual rate limit policy configuration
/// </summary>
public class RateLimitPolicy
{
    /// <summary>
    /// Maximum number of permits in the time window
    /// </summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Time window for the rate limit
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum number of queued requests
    /// </summary>
    [Range(0, int.MaxValue)]
    public int QueueLimit { get; set; } = 10;

    /// <summary>
    /// Auto-replenishment period
    /// </summary>
    public TimeSpan? AutoReplenishment { get; set; }
}

/// <summary>
/// Request size limits configuration
/// </summary>
public class RequestLimitsOptions
{
    /// <summary>
    /// Maximum request body size in bytes (default: 1MB)
    /// </summary>
    [Range(1, long.MaxValue)]
    public long MaxRequestBodySize { get; set; } = 1_048_576; // 1MB

    /// <summary>
    /// Maximum request line size in bytes (default: 8KB)
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxRequestLineSize { get; set; } = 8_192; // 8KB

    /// <summary>
    /// Maximum number of request headers (default: 100)
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxRequestHeaders { get; set; } = 100;

    /// <summary>
    /// Maximum request header total size in bytes (default: 32KB)
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxRequestHeadersTotalSize { get; set; } = 32_768; // 32KB

    /// <summary>
    /// Maximum form collection size in bytes (default: 1MB)
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxRequestFormSize { get; set; } = 1_048_576; // 1MB
}

/// <summary>
/// SSRF protection configuration
/// </summary>
public class SsrfProtectionOptions
{
    /// <summary>
    /// Whether SSRF protection is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Allowed external domains for outbound HTTP requests
    /// </summary>
    public string[] AllowedDomains { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether to block private IP addresses
    /// </summary>
    public bool BlockPrivateNetworks { get; set; } = true;

    /// <summary>
    /// Whether to block localhost addresses
    /// </summary>
    public bool BlockLocalhost { get; set; } = true;

    /// <summary>
    /// Custom blocked IP ranges (CIDR notation)
    /// </summary>
    public string[] CustomBlockedRanges { get; set; } = Array.Empty<string>();
}