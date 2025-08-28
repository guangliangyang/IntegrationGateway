using System.ComponentModel.DataAnnotations;

namespace IntegrationGateway.Api.Configuration.Security;

/// <summary>
/// CORS configuration options
/// </summary>
public class CorsOptions
{
    /// <summary>
    /// Whether CORS is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

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