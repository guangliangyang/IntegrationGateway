using Swashbuckle.AspNetCore.SwaggerUI;

namespace IntegrationGateway.Api.Extensions;

/// <summary>
/// Extension methods for Swagger/OpenAPI middleware configuration
/// Keeps Program.cs clean by encapsulating all Swagger UI setup logic
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>
    /// Configure Swagger middleware conditionally based on environment
    /// </summary>
    public static WebApplication UseConfiguredSwagger(this WebApplication app)
    {
        // Only enable Swagger in development environment
        if (app.Environment.IsDevelopment())
        {
            // Configure Swagger JSON generation
            app.UseSwagger(c =>
            {
                c.RouteTemplate = "swagger/{documentname}/swagger.json";
            });
            
            // Configure Swagger UI
            app.UseSwaggerUI(options =>
            {
                // API endpoint configuration
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Integration Gateway API V1");
                options.SwaggerEndpoint("/swagger/v2/swagger.json", "Integration Gateway API V2");
                
                // UI configuration
                options.RoutePrefix = "swagger";
                options.DocumentTitle = "Integration Gateway API Documentation";
                
                // Display options
                options.DefaultModelsExpandDepth(-1); // Hide schemas section by default
                options.DefaultModelExpandDepth(2);
                options.DocExpansion(DocExpansion.List);
                options.EnableDeepLinking();
                options.DisplayOperationId();
                options.EnableValidator();
                options.ShowExtensions();
                options.EnableFilter();
                options.MaxDisplayedTags(10);
                
                // Custom styling
                options.InjectStylesheet("/swagger-ui/custom.css");
            });
        }

        return app;
    }
}