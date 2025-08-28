using IntegrationGateway.Api.Configuration.Security;

namespace IntegrationGateway.Api.Extensions;

/// <summary>
/// Extension methods for CORS configuration with enable/disable functionality
/// High cohesion: All CORS-related logic in one place
/// Low coupling: Independent of other security features
/// </summary>
public static class CorsExtensions
{
    /// <summary>
    /// Add CORS services with conditional registration based on configuration
    /// </summary>
    public static IServiceCollection AddConfiguredCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsOptions = configuration.GetSection("Security:Cors").Get<CorsOptions>();
        
        // Early return if CORS is disabled - no services registered
        if (corsOptions?.Enabled != true)
        {
            return services;
        }

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                // Configure allowed origins
                if (corsOptions.AllowedOrigins?.Length > 0)
                {
                    policy.WithOrigins(corsOptions.AllowedOrigins);
                }
                else
                {
                    // Fallback for development - still restrictive
                    policy.WithOrigins("https://localhost:3000", "https://localhost:3001");
                }

                // Configure methods and headers
                policy.AllowAnyMethod()
                      .AllowAnyHeader();

                // Configure credentials
                if (corsOptions.AllowCredentials)
                {
                    policy.AllowCredentials();
                }

                // Configure preflight cache
                policy.SetPreflightMaxAge(TimeSpan.FromSeconds(corsOptions.PreflightMaxAge));
            });
        });

        return services;
    }

    /// <summary>
    /// Use CORS middleware conditionally based on configuration
    /// </summary>
    public static WebApplication UseConfiguredCors(this WebApplication app, IConfiguration configuration)
    {
        var corsOptions = configuration.GetSection("Security:Cors").Get<CorsOptions>();
        
        // Only use CORS middleware if enabled
        if (corsOptions?.Enabled == true)
        {
            app.UseCors();
        }

        return app;
    }
}