using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotNetEnv;

namespace IntegrationGateway.Tests.Integration;

/// <summary>
/// Configuration for Application Insights in integration tests
/// Loads configuration from .env file to ensure telemetry is sent to Azure
/// </summary>
public static class TestApplicationInsightsConfiguration
{
    public static void ConfigureApplicationInsights(this IWebHostBuilder builder)
    {
        // Load .env file for Application Insights configuration
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", ".env");
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override Application Insights configuration with .env values
            var appInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
            var appInsightsInstrumentationKey = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_INSTRUMENTATION_KEY");

            if (!string.IsNullOrEmpty(appInsightsConnectionString))
            {
                var inMemoryConfig = new Dictionary<string, string>
                {
                    {"ApplicationInsights:ConnectionString", appInsightsConnectionString},
                    {"ApplicationInsights:InstrumentationKey", appInsightsInstrumentationKey ?? ""},
                    {"ApplicationInsights:EnableAdaptiveSampling", "true"},
                    {"ApplicationInsights:SamplingPercentage", "100.0"},
                    {"ApplicationInsights:EnableHeartbeat", "true"},
                    {"ApplicationInsights:EnableQuickPulseMetricStream", "true"},
                    {"ApplicationInsights:EnableDependencyTracking", "true"},
                    {"ApplicationInsights:EnablePerformanceCounterCollection", "true"},
                    {"ApplicationInsights:MaxTelemetryBufferCapacity", "500"},
                    {"ApplicationInsights:FlushOnDispose", "true"},
                    {"ApplicationInsights:CloudRoleName", "IntegrationGateway-IntegrationTest"},
                    {"ApplicationInsights:CloudRoleInstance", Environment.MachineName + "-Test"},
                    {"ApplicationInsights:CustomProperties:Environment", "IntegrationTest"},
                    {"ApplicationInsights:CustomProperties:Service", "IntegrationGateway-Test"},
                    {"ApplicationInsights:CustomProperties:TestRun", DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss")},
                    // Configure test services
                    {"ErpService:BaseUrl", "http://localhost:5051"},
                    {"WarehouseService:BaseUrl", "http://localhost:5052"},
                    // Enable all features for comprehensive testing
                    {"Cache:Enabled", "true"},
                    {"Idempotency:Enabled", "true"},
                    {"Security:Cors:Enabled", "false"}, // Disable CORS for testing
                    {"Security:RateLimiting:Enabled", "false"}, // Disable rate limiting for testing
                    {"Security:SsrfProtection:Enabled", "false"} // Disable SSRF protection for testing
                };

                config.AddInMemoryCollection(inMemoryConfig);
            }
        });

        builder.ConfigureServices(services =>
        {
            // Add custom telemetry initializer for test identification
            services.AddSingleton<Microsoft.ApplicationInsights.Extensibility.ITelemetryInitializer, TestTelemetryInitializer>();
        });
    }
}

/// <summary>
/// Custom telemetry initializer to mark telemetry as coming from integration tests
/// </summary>
public class TestTelemetryInitializer : Microsoft.ApplicationInsights.Extensibility.ITelemetryInitializer
{
    public void Initialize(Microsoft.ApplicationInsights.Channel.ITelemetry telemetry)
    {
        telemetry.Context.GlobalProperties["TestSource"] = "IntegrationTests";
        telemetry.Context.GlobalProperties["TestEnvironment"] = "Local";
        telemetry.Context.GlobalProperties["TestTimestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
    }
}