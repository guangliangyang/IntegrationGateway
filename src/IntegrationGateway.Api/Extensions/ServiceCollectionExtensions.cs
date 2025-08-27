using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using IntegrationGateway.Api.Configuration;
using IntegrationGateway.Services.Configuration;

namespace IntegrationGateway.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure JWT Authentication
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
        if (jwtOptions != null && !string.IsNullOrEmpty(jwtOptions.SecretKey))
        {
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
        }

        return services;
    }

    /// <summary>
    /// Configure HTTP clients with resilience policies
    /// </summary>
    public static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        var erpOptions = configuration.GetSection(ErpServiceOptions.SectionName).Get<ErpServiceOptions>();
        var warehouseOptions = configuration.GetSection(WarehouseServiceOptions.SectionName).Get<WarehouseServiceOptions>();

        // Configure ERP HTTP client
        services.AddHttpClient("ErpClient", client =>
            ConfigureHttpClient(client, erpOptions?.BaseUrl ?? "http://localhost:5001", 
                              erpOptions?.TimeoutSeconds ?? 30, erpOptions?.ApiKey))
            .AddPolicyHandler(GetRetryPolicy(erpOptions?.MaxRetries ?? 3))
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(GetTimeoutPolicy(erpOptions?.TimeoutSeconds ?? 30));

        // Configure Warehouse HTTP client
        services.AddHttpClient("WarehouseClient", client =>
            ConfigureHttpClient(client, warehouseOptions?.BaseUrl ?? "http://localhost:5002",
                              warehouseOptions?.TimeoutSeconds ?? 30, warehouseOptions?.ApiKey))
            .AddPolicyHandler(GetRetryPolicy(warehouseOptions?.MaxRetries ?? 3))
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(GetTimeoutPolicy(warehouseOptions?.TimeoutSeconds ?? 30));

        return services;
    }

    /// <summary>
    /// Configure common HTTP client settings
    /// </summary>
    private static void ConfigureHttpClient(HttpClient client, string baseUrl, int timeoutSeconds, string? apiKey)
    {
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
        
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", "IntegrationGateway/1.0");
    }

    /// <summary>
    /// Get retry policy with exponential backoff and jitter
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int maxRetries)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100))); // Jitter
    }

    /// <summary>
    /// Get circuit breaker policy
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Get timeout policy
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int timeoutSeconds)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(timeoutSeconds);
    }
}