using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Channel;
using IntegrationGateway.Api.Configuration.Authentication;
using IntegrationGateway.Api.Configuration.Middleware;
using IntegrationGateway.Api.Configuration.Telemetry;

namespace IntegrationGateway.Api.Extensions;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Add Azure Key Vault configuration provider
    /// </summary>
    public static WebApplicationBuilder AddAzureKeyVault(this WebApplicationBuilder builder)
    {
        // 先处理开发环境，直接返回
        if (builder.Environment.IsDevelopment())
        {
            Console.WriteLine("Development environment detected, skipping Key Vault integration");
            return builder;
        }

        var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
        if (string.IsNullOrEmpty(keyVaultUri))
        {
            Console.WriteLine("KeyVault:VaultUri not configured, skipping Key Vault integration");
            return builder;
        }

        try
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri),
                new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    // Exclude interactive browser credential for server environments
                    ExcludeInteractiveBrowserCredential = true,
                    // Use managed identity in Azure, Azure CLI for local development
                    ExcludeSharedTokenCacheCredential = true,
                    ExcludeVisualStudioCredential = true,
                    ExcludeVisualStudioCodeCredential = true
                }));

            Console.WriteLine($"Successfully configured Azure Key Vault: {keyVaultUri}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to configure Azure Key Vault: {ex.Message}");
            // Don't fail the application if Key Vault is not available
            // Fall back to other configuration sources
        }

        return builder;
    }

    /// <summary>
    /// Add Application Insights telemetry
    /// </summary>
    public static WebApplicationBuilder AddApplicationInsights(this WebApplicationBuilder builder)
    {
        var appInsightsOptions = builder.Configuration.GetSection(ApplicationInsightsOptions.SectionName)
            .Get<ApplicationInsightsOptions>();

        if (appInsightsOptions == null || string.IsNullOrEmpty(appInsightsOptions.ConnectionString))
        {
            Console.WriteLine("Application Insights connection string not found, telemetry disabled");
            return builder;
        }

        // Add Application Insights telemetry
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = appInsightsOptions.ConnectionString;
            options.EnableAdaptiveSampling = appInsightsOptions.EnableAdaptiveSampling;
            options.EnableQuickPulseMetricStream = appInsightsOptions.EnableQuickPulseMetricStream;
            options.EnableHeartbeat = appInsightsOptions.EnableHeartbeat;
            options.EnablePerformanceCounterCollectionModule = appInsightsOptions.EnablePerformanceCounterCollection;
            options.EnableDependencyTrackingTelemetryModule = appInsightsOptions.EnableDependencyTracking;
        });

        // Configure basic telemetry
        builder.Services.Configure<TelemetryConfiguration>(config =>
        {
            // Set cloud role name for distributed tracing
            config.TelemetryInitializers.Add(new CloudRoleNameInitializer(appInsightsOptions.CloudRoleName));
            
            // Add custom properties
            if (appInsightsOptions.CustomProperties.Any())
            {
                config.TelemetryInitializers.Add(new CustomPropertiesInitializer(appInsightsOptions.CustomProperties));
            }

            // Configure sampling if specified
            if (!appInsightsOptions.EnableAdaptiveSampling && appInsightsOptions.SamplingPercentage < 100.0)
            {
                config.DefaultTelemetrySink.TelemetryProcessorChainBuilder
                    .UseSampling(appInsightsOptions.SamplingPercentage)
                    .Build();
            }
        });

        Console.WriteLine($"Application Insights configured with connection string: {appInsightsOptions.ConnectionString[..20]}...");

        return builder;
    }

    /// <summary>
    /// Add configuration validation
    /// </summary>
    public static WebApplicationBuilder AddConfigurationValidation(this WebApplicationBuilder builder)
    {
        // Validate critical configuration options at startup
        builder.Services.AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .Validate(config => !string.IsNullOrEmpty(config.SecretKey), 
                      "JWT SecretKey is required but not configured")
            .Validate(config => config.SecretKey.Length >= 32, 
                      "JWT SecretKey must be at least 32 characters long")
            .ValidateOnStart();

        builder.Services.AddOptions<ApplicationInsightsOptions>()
            .Bind(builder.Configuration.GetSection(ApplicationInsightsOptions.SectionName))
            .Validate(config => builder.Environment.IsDevelopment() || !string.IsNullOrEmpty(config.ConnectionString), 
                      "Application Insights ConnectionString is required in non-development environments")
            .ValidateOnStart();

        builder.Services.AddOptions<IdempotencyOptions>()
            .Bind(builder.Configuration.GetSection(IdempotencyOptions.SectionName))
            .Validate(config => config.DefaultExpirationTime > TimeSpan.Zero, 
                      "Idempotency DefaultExpirationTime must be greater than zero")
            .Validate(config => config.MaxConcurrentOperations > 0, 
                      "Idempotency MaxConcurrentOperations must be greater than zero")
            .ValidateOnStart();

        return builder;
    }
}

/// <summary>
/// Telemetry initializer to set cloud role name
/// </summary>
public class CloudRoleNameInitializer : ITelemetryInitializer
{
    private readonly string _roleName;

    public CloudRoleNameInitializer(string roleName)
    {
        _roleName = roleName;
    }

    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName = _roleName;
    }
}

/// <summary>
/// Telemetry initializer to add custom properties
/// </summary>
public class CustomPropertiesInitializer : ITelemetryInitializer
{
    private readonly Dictionary<string, string> _customProperties;

    public CustomPropertiesInitializer(Dictionary<string, string> customProperties)
    {
        _customProperties = customProperties;
    }

    public void Initialize(ITelemetry telemetry)
    {
        foreach (var property in _customProperties)
        {
            if (!telemetry.Context.GlobalProperties.ContainsKey(property.Key))
            {
                telemetry.Context.GlobalProperties[property.Key] = property.Value;
            }
        }
    }
}