using IntegrationGateway.Api.Configuration.Security;

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

