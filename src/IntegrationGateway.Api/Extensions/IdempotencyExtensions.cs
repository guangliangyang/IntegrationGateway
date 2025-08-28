using IntegrationGateway.Api.Configuration;
using IntegrationGateway.Api.Middleware;

namespace IntegrationGateway.Api.Extensions;

/// <summary>
/// Extension methods for Idempotency middleware configuration
/// High cohesion: All idempotency logic in one place
/// Low coupling: Independent of other middleware features
/// </summary>
public static class IdempotencyExtensions
{
    /// <summary>
    /// Use idempotency middleware conditionally based on configuration
    /// Note: The middleware itself checks the Enabled flag internally for better testability
    /// </summary>
    public static WebApplication UseConfiguredIdempotency(this WebApplication app, IConfiguration configuration)
    {
        var idempotencyOptions = configuration.GetSection(IdempotencyOptions.SectionName).Get<IdempotencyOptions>();
        
        // Always add middleware - let it handle enabled/disabled internally
        // This approach is better for unit testing as the middleware can be tested independently
        app.UseMiddleware<IdempotencyMiddleware>();

        return app;
    }
}