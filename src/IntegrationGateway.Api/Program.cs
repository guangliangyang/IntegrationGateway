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
using DotNetEnv;

[assembly: InternalsVisibleTo("IntegrationGateway.Tests")]

// Load environment variables from .env file
Env.Load();

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
builder.Services.Configure<CircuitBreakerOptions>(builder.Configuration.GetSection(CircuitBreakerOptions.SectionName));
builder.Services.Configure<HttpClientOptions>(builder.Configuration.GetSection(HttpClientOptions.SectionName));

// Add services to the container
builder.Services.AddControllers();


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

// Add HTTP clients with SSRF protection
builder.Services.AddConfiguredSsrfProtection(builder.Configuration);
builder.Services.AddHttpClients(builder.Configuration);

// Register services with IHttpClientFactory
builder.Services.AddScoped<IErpService, ErpService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();

// Add application services
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddScoped<IProductService, ProductService>();
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

// Configure request size limits
var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>();
var requestLimits = securityOptions?.RequestLimits ?? new RequestLimitsOptions();

builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = requestLimits.MaxRequestBodySize;
    options.Limits.MaxRequestLineSize = requestLimits.MaxRequestLineSize;
    options.Limits.MaxRequestHeaderCount = requestLimits.MaxRequestHeaders;
    options.Limits.MaxRequestHeadersTotalSize = requestLimits.MaxRequestHeadersTotalSize;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = requestLimits.MaxRequestFormSize;
    options.ValueLengthLimit = requestLimits.MaxRequestFormSize;
});

// Add security features conditionally
builder.Services.AddConfiguredRateLimiting(builder.Configuration);
builder.Services.AddConfiguredCors(builder.Configuration);

// CORS is now handled by AddConfiguredCors extension

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

// Use security features conditionally (high cohesion, low coupling)
app.UseConfiguredRateLimiting(builder.Configuration);
app.UseConfiguredCors(builder.Configuration);

app.UseAuthentication();
app.UseAuthorization();

// Use idempotency middleware conditionally
app.UseConfiguredIdempotency(builder.Configuration);

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