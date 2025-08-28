namespace IntegrationGateway.Api.Configuration;

public class ApplicationInsightsOptions
{
    public const string SectionName = "ApplicationInsights";
    
    /// <summary>
    /// Application Insights connection string (should be stored in Key Vault)
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// Legacy instrumentation key (for backward compatibility)
    /// </summary>
    public string InstrumentationKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Enable adaptive sampling to reduce telemetry volume
    /// </summary>
    public bool EnableAdaptiveSampling { get; set; } = true;
    
    /// <summary>
    /// Sampling percentage (0.1 to 100.0)
    /// </summary>
    public double SamplingPercentage { get; set; } = 100.0;
    
    /// <summary>
    /// Enable heartbeat telemetry
    /// </summary>
    public bool EnableHeartbeat { get; set; } = true;
    
    /// <summary>
    /// Enable live metrics stream
    /// </summary>
    public bool EnableQuickPulseMetricStream { get; set; } = true;
    
    /// <summary>
    /// Enable dependency tracking
    /// </summary>
    public bool EnableDependencyTracking { get; set; } = true;
    
    /// <summary>
    /// Enable performance counter collection
    /// </summary>
    public bool EnablePerformanceCounterCollection { get; set; } = true;
    
    /// <summary>
    /// Maximum telemetry buffer capacity
    /// </summary>
    public int MaxTelemetryBufferCapacity { get; set; } = 500;
    
    /// <summary>
    /// Flush telemetry on dispose
    /// </summary>
    public bool FlushOnDispose { get; set; } = true;
    
    /// <summary>
    /// Cloud role name for distributed tracing
    /// </summary>
    public string CloudRoleName { get; set; } = "IntegrationGateway";
    
    /// <summary>
    /// Cloud role instance
    /// </summary>
    public string CloudRoleInstance { get; set; } = string.Empty;
    
    /// <summary>
    /// Custom properties to add to all telemetry
    /// </summary>
    public Dictionary<string, string> CustomProperties { get; set; } = new();
}