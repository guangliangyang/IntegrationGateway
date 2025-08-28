using IntegrationGateway.Api.Configuration.Security;
using IntegrationGateway.Api.Services;

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
/// HTTP message handler for SSRF protection
/// </summary>
public class SsrfProtectionHandler : DelegatingHandler
{
    private readonly IUrlValidationService _urlValidationService;
    private readonly ILogger<SsrfProtectionHandler> _logger;

    public SsrfProtectionHandler(
        IUrlValidationService urlValidationService,
        ILogger<SsrfProtectionHandler> logger)
    {
        _urlValidationService = urlValidationService;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri == null)
        {
            _logger.LogError("HTTP request has no URI");
            throw new InvalidOperationException("Request URI cannot be null");
        }

        // Validate URL before making the request
        var isUrlSafe = await _urlValidationService.IsUrlSafeAsync(request.RequestUri);
        if (!isUrlSafe)
        {
            _logger.LogWarning("Blocked potentially unsafe URL: {Url}", request.RequestUri);
            throw new HttpRequestException($"Request to '{request.RequestUri}' was blocked by SSRF protection");
        }

        return await base.SendAsync(request, cancellationToken);
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