using System.ComponentModel.DataAnnotations;

namespace IntegrationGateway.Api.Configuration.Security;

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