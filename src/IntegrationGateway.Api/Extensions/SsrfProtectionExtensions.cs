using IntegrationGateway.Api.Configuration;
using IntegrationGateway.Api.Services;
using static IntegrationGateway.Api.Extensions.ServiceCollectionExtensions;

namespace IntegrationGateway.Api.Extensions;

/// <summary>
/// Extension methods for SSRF Protection configuration
/// High cohesion: All SSRF protection logic in one place
/// Low coupling: Independent of other security features
/// </summary>
public static class SsrfProtectionExtensions
{
    /// <summary>
    /// Add SSRF protection services conditionally based on configuration
    /// </summary>
    public static IServiceCollection AddConfiguredSsrfProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var ssrfOptions = configuration.GetSection("Security:SsrfProtection").Get<SsrfProtectionOptions>();
        
        // Always register URL validation service - it handles enabled/disabled internally
        services.AddSingleton<IUrlValidationService, UrlValidationService>();
        
        // Only register SSRF protection handler if enabled
        if (ssrfOptions?.Enabled == true)
        {
            services.AddTransient<SsrfProtectionHandler>();
        }
        else
        {
            // Register no-op handler when SSRF protection is disabled
            services.AddTransient<NoOpSsrfProtectionHandler>();
        }

        return services;
    }
}

/// <summary>
/// No-operation SSRF protection handler for when SSRF protection is disabled
/// Allows requests to pass through without validation
/// </summary>
public class NoOpSsrfProtectionHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Pass through all requests without validation when SSRF protection is disabled
        return base.SendAsync(request, cancellationToken);
    }
}