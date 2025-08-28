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
        var circuitBreakerOptions = configuration.GetSection(CircuitBreakerOptions.SectionName).Get<CircuitBreakerOptions>();
        var httpClientOptions = configuration.GetSection(HttpClientOptions.SectionName).Get<HttpClientOptions>();

        // Configure ERP HTTP client
        services.AddHttpClient("ErpClient", client =>
            ConfigureHttpClient(client, erpOptions?.BaseUrl ?? Environment.GetEnvironmentVariable("ERP_BASE_URL") ?? "http://localhost:5001", 
                              erpOptions?.TimeoutSeconds ?? httpClientOptions?.DefaultConnectionTimeoutSeconds ?? 30, 
                              erpOptions?.ApiKey, httpClientOptions))
            .AddPolicyHandler(GetRetryPolicy(erpOptions?.MaxRetries ?? 3))
            .AddPolicyHandler(GetCircuitBreakerPolicy(circuitBreakerOptions))
            .AddPolicyHandler(GetTimeoutPolicy(erpOptions?.TimeoutSeconds ?? httpClientOptions?.DefaultRequestTimeoutSeconds ?? 30));

        // Configure Warehouse HTTP client
        services.AddHttpClient("WarehouseClient", client =>
            ConfigureHttpClient(client, warehouseOptions?.BaseUrl ?? Environment.GetEnvironmentVariable("WAREHOUSE_BASE_URL") ?? "http://localhost:5002",
                              warehouseOptions?.TimeoutSeconds ?? httpClientOptions?.DefaultConnectionTimeoutSeconds ?? 30, 
                              warehouseOptions?.ApiKey, httpClientOptions))
            .AddPolicyHandler(GetRetryPolicy(warehouseOptions?.MaxRetries ?? 3))
            .AddPolicyHandler(GetCircuitBreakerPolicy(circuitBreakerOptions))
            .AddPolicyHandler(GetTimeoutPolicy(warehouseOptions?.TimeoutSeconds ?? httpClientOptions?.DefaultRequestTimeoutSeconds ?? 30));

        return services;
    }

    /// <summary>
    /// Configure common HTTP client settings
    /// </summary>
    private static void ConfigureHttpClient(HttpClient client, string baseUrl, int timeoutSeconds, string? apiKey, HttpClientOptions? httpClientOptions)
    {
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
        
        // Use configurable headers
        var acceptHeader = httpClientOptions?.AcceptHeader ?? "application/json";
        var userAgent = httpClientOptions?.UserAgent ?? "IntegrationGateway/1.0";
        
        client.DefaultRequestHeaders.Add("Accept", acceptHeader);
        client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        
        // Add custom headers from configuration
        if (httpClientOptions?.DefaultHeaders != null)
        {
            foreach (var header in httpClientOptions.DefaultHeaders)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }
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
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(CircuitBreakerOptions? options)
    {
        var failureThreshold = options?.FailureThreshold ?? 5;
        var breakDuration = options?.BreakDuration ?? TimeSpan.FromMinutes(1);
        
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: failureThreshold,
                durationOfBreak: breakDuration);
    }

    /// <summary>
    /// Get timeout policy
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int timeoutSeconds)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(timeoutSeconds);
    }
}