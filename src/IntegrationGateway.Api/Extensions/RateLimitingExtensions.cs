using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using IntegrationGateway.Api.Configuration;

namespace IntegrationGateway.Api.Extensions;

/// <summary>
/// Extension methods for Rate Limiting configuration
/// High cohesion: All rate limiting logic in one place
/// Low coupling: Independent of other security features
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Add rate limiting services with conditional registration
    /// </summary>
    public static IServiceCollection AddConfiguredRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitConfig = configuration.GetSection("Security:RateLimiting").Get<RateLimitingOptions>();
        
        // Early return if rate limiting is disabled
        if (rateLimitConfig?.Enabled != true)
        {
            // Still add rate limiter but with no-op configuration
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.CreateChained<HttpContext>();
            });
            return services;
        }

        services.AddRateLimiter(options =>
        {
            // General API rate limiting by IP
            options.AddFixedWindowLimiter("GeneralApi", limiterOptions =>
            {
                limiterOptions.PermitLimit = rateLimitConfig.GeneralApi.PermitLimit;
                limiterOptions.Window = rateLimitConfig.GeneralApi.Window;
                limiterOptions.QueueLimit = rateLimitConfig.GeneralApi.QueueLimit;
                limiterOptions.AutoReplenishment = rateLimitConfig.GeneralApi.AutoReplenishment.HasValue;
            });
            
            // Authentication rate limiting by IP
            options.AddFixedWindowLimiter("Authentication", limiterOptions =>
            {
                limiterOptions.PermitLimit = rateLimitConfig.Authentication.PermitLimit;
                limiterOptions.Window = rateLimitConfig.Authentication.Window;
                limiterOptions.QueueLimit = rateLimitConfig.Authentication.QueueLimit;
                limiterOptions.AutoReplenishment = rateLimitConfig.Authentication.AutoReplenishment.HasValue;
            });
            
            // Write operations rate limiting by user
            options.AddFixedWindowLimiter("WriteOperations", limiterOptions =>
            {
                limiterOptions.PermitLimit = rateLimitConfig.WriteOperations.PermitLimit;
                limiterOptions.Window = rateLimitConfig.WriteOperations.Window;
                limiterOptions.QueueLimit = rateLimitConfig.WriteOperations.QueueLimit;
                limiterOptions.AutoReplenishment = rateLimitConfig.WriteOperations.AutoReplenishment.HasValue;
            });
            
            // Global limiter - applies GeneralApi policy to all requests by default
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitConfig.GeneralApi.PermitLimit,
                        Window = rateLimitConfig.GeneralApi.Window,
                        QueueLimit = rateLimitConfig.GeneralApi.QueueLimit,
                        AutoReplenishment = rateLimitConfig.GeneralApi.AutoReplenishment.HasValue
                    }));
            
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Response.ContentType = "application/json";
                
                var response = new
                {
                    type = "rate_limit_exceeded",
                    title = "Too Many Requests",
                    detail = "Rate limit exceeded. Please try again later.",
                    status = 429,
                    traceId = context.HttpContext.TraceIdentifier
                };
                
                await context.HttpContext.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    }), token);
            };
        });

        return services;
    }

    /// <summary>
    /// Use rate limiting middleware conditionally
    /// </summary>
    public static WebApplication UseConfiguredRateLimiting(this WebApplication app, IConfiguration configuration)
    {
        var rateLimitConfig = configuration.GetSection("Security:RateLimiting").Get<RateLimitingOptions>();
        
        // Always use rate limiter (it handles enabled/disabled internally)
        app.UseRateLimiter();

        return app;
    }
}