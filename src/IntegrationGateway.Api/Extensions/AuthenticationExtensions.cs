using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using IntegrationGateway.Api.Configuration.Authentication;

namespace IntegrationGateway.Api.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Configure JWT Authentication
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        
        // Validate JWT configuration more strictly
        if (jwtOptions == null)
        {
            if (environment == "Production")
            {
                throw new InvalidOperationException("JWT configuration is required in production environment but was not found.");
            }
            // In non-production environments, we can skip JWT auth if not configured
            return services;
        }

        if (string.IsNullOrEmpty(jwtOptions.SecretKey))
        {
            if (environment == "Production")
            {
                throw new InvalidOperationException("JWT SecretKey is required in production environment but was not provided.");
            }
            // In non-production environments, we can skip JWT auth if SecretKey is missing
            return services;
        }

        // Additional validation for production
        if (environment == "Production")
        {
            if (jwtOptions.SecretKey.Length < 32)
            {
                throw new InvalidOperationException("JWT SecretKey must be at least 32 characters long in production environment.");
            }
            
            if (string.IsNullOrEmpty(jwtOptions.Issuer))
            {
                throw new InvalidOperationException("JWT Issuer is required in production environment.");
            }
            
            if (string.IsNullOrEmpty(jwtOptions.Audience))
            {
                throw new InvalidOperationException("JWT Audience is required in production environment.");
            }
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = jwtOptions.ValidateIssuer,
                    ValidateAudience = jwtOptions.ValidateAudience,
                    ValidateLifetime = jwtOptions.ValidateLifetime,
                    ValidateIssuerSigningKey = jwtOptions.ValidateIssuerSigningKey,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
                };
            });

        return services;
    }
}