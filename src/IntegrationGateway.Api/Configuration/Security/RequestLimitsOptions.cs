using System.ComponentModel.DataAnnotations;

namespace IntegrationGateway.Api.Configuration.Security;

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