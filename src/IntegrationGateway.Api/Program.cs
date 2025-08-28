using System.Runtime.CompilerServices;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using IntegrationGateway.Api.Configuration;
using IntegrationGateway.Api.Middleware;
using IntegrationGateway.Api.Extensions;
using IntegrationGateway.Services.Configuration;
using IntegrationGateway.Services.Implementation;
using IntegrationGateway.Services.Interfaces;
using IntegrationGateway.Application;

[assembly: InternalsVisibleTo("IntegrationGateway.Tests")]

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault configuration
builder.AddAzureKeyVault();

// Add Application Insights telemetry
builder.AddApplicationInsights();

// Add configuration validation
builder.AddConfigurationValidation();

// Add security configuration
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));

// Add configuration
builder.Services.Configure<ErpServiceOptions>(builder.Configuration.GetSection(ErpServiceOptions.SectionName));
builder.Services.Configure<WarehouseServiceOptions>(builder.Configuration.GetSection(WarehouseServiceOptions.SectionName));
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.Configure<CircuitBreakerOptions>(builder.Configuration.GetSection(CircuitBreakerOptions.SectionName));
builder.Services.Configure<HttpClientOptions>(builder.Configuration.GetSection(HttpClientOptions.SectionName));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new QueryStringApiVersionReader("version"),
        new HeaderApiVersionReader("X-API-Version"));
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Add HTTP clients with centralized resilience policies
builder.Services.AddHttpClients(builder.Configuration);

// Register services with IHttpClientFactory
builder.Services.AddScoped<IErpService, ErpService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();

// Add application services
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddSingleton<IIdempotencyService, IdempotencyService>();

// Add HttpContextAccessor and CurrentUser service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IntegrationGateway.Application.Common.Interfaces.ICurrentUserService, IntegrationGateway.Api.Services.CurrentUserService>();

// Add JWT authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureSwagger();

// Add health checks
builder.Services.AddHealthChecks();

// Configure Kestrel server limits
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>();
    var requestLimits = securityOptions?.RequestLimits ?? new RequestLimitsOptions();
    
    options.Limits.MaxRequestBodySize = requestLimits.MaxRequestBodySize;
    options.Limits.MaxRequestLineSize = requestLimits.MaxRequestLineSize;
    options.Limits.MaxRequestHeaderCount = requestLimits.MaxRequestHeaders;
    options.Limits.MaxRequestHeadersTotalSize = requestLimits.MaxRequestHeadersTotalSize;
});

// Configure form options
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>();
    var requestLimits = securityOptions?.RequestLimits ?? new RequestLimitsOptions();
    
    options.MultipartBodyLengthLimit = requestLimits.MaxRequestFormSize;
    options.ValueLengthLimit = requestLimits.MaxRequestFormSize;
});

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    var securityConfig = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>();
    var rateLimitConfig = securityConfig?.RateLimiting ?? new RateLimitingOptions();
    
    if (!rateLimitConfig.Enabled)
    {
        options.GlobalLimiter = PartitionedRateLimiter.CreateChained<HttpContext>();
        return;
    }

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

// Add CORS with security-enhanced configuration
builder.Services.AddCors(options =>
{
    var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>();
    var corsOptions = securityOptions?.Cors ?? new CorsOptions();
    
    options.AddDefaultPolicy(policy =>
    {
        if (corsOptions.AllowedOrigins?.Length > 0)
        {
            policy.WithOrigins(corsOptions.AllowedOrigins);
        }
        else
        {
            // Fallback for development - still restrictive
            policy.WithOrigins("https://localhost:3000", "https://localhost:3001");
        }
        
        policy.AllowAnyMethod()
              .AllowAnyHeader();
              
        if (corsOptions.AllowCredentials)
        {
            policy.AllowCredentials();
        }
        
        policy.SetPreflightMaxAge(TimeSpan.FromSeconds(corsOptions.PreflightMaxAge));
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "swagger/{documentname}/swagger.json";
    });
    
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Integration Gateway API V1");
        options.SwaggerEndpoint("/swagger/v2/swagger.json", "Integration Gateway API V2");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Integration Gateway API Documentation";
        options.DefaultModelsExpandDepth(-1); // Hide schemas section by default
        options.DefaultModelExpandDepth(2);
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        options.EnableDeepLinking();
        options.DisplayOperationId();
        options.EnableValidator();
        options.ShowExtensions();
        options.EnableFilter();
        options.MaxDisplayedTags(10);
        
        // Custom CSS for better appearance
        options.InjectStylesheet("/swagger-ui/custom.css");
    });
}

app.UseHttpsRedirection();

// Add rate limiting middleware
app.UseRateLimiter();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// Add custom middleware
// High-concurrency idempotency middleware with per-operation locking
app.UseMiddleware<IdempotencyMiddleware>();

app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health");

// Add a simple root endpoint
app.MapGet("/", () => new
{
    Service = "Integration Gateway",
    Version = "1.0.0",
    Status = "Running",
    Timestamp = DateTime.UtcNow,
    ApiDocumentation = "/swagger",
    Endpoints = new
    {
        Health = "/health",
        V1_Products = "/api/v1/products",
        V2_Products = "/api/v2/products",
        OpenAPI_V1 = "/swagger/v1/swagger.json",
        OpenAPI_V2 = "/swagger/v2/swagger.json"
    }
});

app.Run();

public partial class Program { }