namespace IntegrationGateway.Api.Configuration.Integration;

public class HttpClientOptions
{
    public const string SectionName = "HttpClient";
    
    /// <summary>
    /// Default User-Agent header value
    /// </summary>
    public string UserAgent { get; set; } = "IntegrationGateway/1.0";
    
    /// <summary>
    /// Default Accept header value
    /// </summary>
    public string AcceptHeader { get; set; } = "application/json";
    
    /// <summary>
    /// Default Content-Type header value
    /// </summary>
    public string ContentTypeHeader { get; set; } = "application/json; charset=utf-8";
    
    /// <summary>
    /// Default connection timeout in seconds
    /// </summary>
    public int DefaultConnectionTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Default request timeout in seconds
    /// </summary>
    public int DefaultRequestTimeoutSeconds { get; set; } = 120;
    
    /// <summary>
    /// Enable automatic decompression
    /// </summary>
    public bool EnableAutomaticDecompression { get; set; } = true;
    
    /// <summary>
    /// Maximum number of connections per server
    /// </summary>
    public int MaxConnectionsPerServer { get; set; } = 10;
    
    /// <summary>
    /// Pool connection lifetime in minutes
    /// </summary>
    public int PoolConnectionLifetimeMinutes { get; set; } = 5;
    
    /// <summary>
    /// Custom headers to add to all requests
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}