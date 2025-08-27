using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using IntegrationGateway.Api.Configuration;
using IntegrationGateway.Api.Middleware;
using IntegrationGateway.Api.Extensions;
using IntegrationGateway.Services.Configuration;
using IntegrationGateway.Services.Implementation;
using IntegrationGateway.Services.Interfaces;
using IntegrationGateway.Application;

[assembly: InternalsVisibleTo("IntegrationGateway.Tests")]

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Services.Configure<ErpServiceOptions>(builder.Configuration.GetSection(ErpServiceOptions.SectionName));
builder.Services.Configure<WarehouseServiceOptions>(builder.Configuration.GetSection(WarehouseServiceOptions.SectionName));
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.Configure<CircuitBreakerOptions>(builder.Configuration.GetSection(CircuitBreakerOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

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

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
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