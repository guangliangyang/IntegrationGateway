namespace IntegrationGateway.Api.Configuration.Security;

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