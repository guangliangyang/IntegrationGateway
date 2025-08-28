using System.Reflection;
using FluentValidation;
using IntegrationGateway.Application.Common.Behaviours;
using IntegrationGateway.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationGateway.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register AutoMapper - remove conflicting package
        // services.AddAutoMapper(Assembly.GetExecutingAssembly());

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Check if caching is enabled
        var cacheEnabled = configuration.GetValue<bool>("Cache:Enabled", false);
        
        // Add cache services conditionally
        if (cacheEnabled)
        {
            services.AddMemoryCache();
            services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
        }

        // Register MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            
            // Register Pipeline Behaviors - ORDER MATTERS!
            // 1. Unhandled Exception Behavior (outermost - catches all exceptions)
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            
            // 2. Validation Behavior (validate input before processing)
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            
            // 3. Performance Behavior (measure execution time)
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
            
            // 4. Logging Behavior (log request/response details)
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
            
            // 5. Caching Behavior (cache responses - closest to handler) - only if enabled
            if (cacheEnabled)
            {
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));
            }
        });

        // Configure Performance Behavior options
        services.Configure<PerformanceOptions>(configuration.GetSection("Performance"));

        return services;
    }
}