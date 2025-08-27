using Microsoft.OpenApi.Models;
using System.Reflection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace IntegrationGateway.Api.Configuration;

public static class SwaggerConfiguration
{
    public static void ConfigureSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // V1 API Documentation
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Integration Gateway API",
                Version = "v1.0",
                Description = @"
## Overview

The Integration Gateway API provides a unified interface for managing products by orchestrating data from ERP and Warehouse systems.

## Features

- **Product Management**: Full CRUD operations for products
- **Resilience Patterns**: Built-in retry logic, circuit breakers, and timeouts
- **Idempotency**: Guaranteed idempotent operations using Idempotency-Key headers
- **Caching**: Intelligent caching with TTL for improved performance
- **Security**: JWT Bearer token authentication
- **Error Handling**: Comprehensive error responses with detailed information

## Authentication

All write operations (POST, PUT, DELETE) require JWT Bearer token authentication.

```
Authorization: Bearer <your-jwt-token>
```

## Idempotency

All write operations require an `Idempotency-Key` header to ensure operations can be safely retried.

```
Idempotency-Key: <unique-key>
```

## Rate Limiting

API requests are subject to rate limiting to ensure service stability.

## Error Handling

The API returns structured error responses following RFC 7807 (Problem Details for HTTP APIs):

```json
{
  ""type"": ""error_type"",
  ""title"": ""Error Title"",
  ""detail"": ""Detailed error description"",
  ""status"": 400,
  ""traceId"": ""trace-identifier""
}
```",
                Contact = new OpenApiContact
                {
                    Name = "Integration Gateway Team",
                    Email = "support@integrationgateway.com",
                    Url = new Uri("https://docs.integrationgateway.com")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            // V2 API Documentation
            options.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "Integration Gateway API",
                Version = "v2.0",
                Description = @"
## Overview

Version 2 of the Integration Gateway API provides enhanced product information including supplier details, tags, and metadata.

## What's New in V2

- **Enhanced Product Information**: Additional fields for supplier, tags, and custom metadata
- **Backward Compatibility**: All V1 operations are supported with enhanced responses
- **Improved Metadata**: Rich metadata support for extended product information
- **Performance Optimizations**: Improved caching and data aggregation

## Migration from V1

V2 API is backward compatible with V1. Simply change your base URL from `/api/v1/` to `/api/v2/` to receive enhanced responses.

## Authentication & Idempotency

Same requirements as V1 - JWT Bearer tokens and Idempotency-Key headers for write operations.",
                Contact = new OpenApiContact
                {
                    Name = "Integration Gateway Team",
                    Email = "support@integrationgateway.com",
                    Url = new Uri("https://docs.integrationgateway.com")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            // Add security definition for JWT
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = @"
JWT Authorization header using the Bearer scheme.

Enter 'Bearer' [space] and then your token in the text input below.

Example: 'Bearer 12345abcdef'",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Add custom operation filter for idempotency headers
            options.OperationFilter<IdempotencyHeaderOperationFilter>();
            
            // Add custom schema filters
            options.SchemaFilter<ExampleSchemaFilter>();

            // Include XML documentation
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            // Configure Swagger UI
            options.EnableAnnotations();
        });
    }
}

/// <summary>
/// Operation filter to add Idempotency-Key header to POST and PUT operations
/// </summary>
public class IdempotencyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var httpMethod = context.ApiDescription.HttpMethod?.ToUpper();
        
        if (httpMethod == "POST" || httpMethod == "PUT")
        {
            operation.Parameters ??= new List<OpenApiParameter>();
            
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = true,
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Format = "uuid"
                },
                Description = "Unique identifier for idempotent operations. Use the same key to safely retry the same operation.",
                Example = new Microsoft.OpenApi.Any.OpenApiString("123e4567-e89b-12d3-a456-426614174000")
            });
        }
    }
}

/// <summary>
/// Schema filter to add examples to data models
/// </summary>
public class ExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.Name == "ProductDto")
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["id"] = new Microsoft.OpenApi.Any.OpenApiString("prod-001"),
                ["name"] = new Microsoft.OpenApi.Any.OpenApiString("Wireless Headphones"),
                ["description"] = new Microsoft.OpenApi.Any.OpenApiString("High-quality wireless headphones with noise cancellation"),
                ["price"] = new Microsoft.OpenApi.Any.OpenApiDouble(199.99),
                ["category"] = new Microsoft.OpenApi.Any.OpenApiString("Electronics"),
                ["isActive"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true),
                ["stockQuantity"] = new Microsoft.OpenApi.Any.OpenApiInteger(25),
                ["inStock"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true),
                ["warehouseLocation"] = new Microsoft.OpenApi.Any.OpenApiString("Warehouse-A-01")
            };
        }
        else if (context.Type.Name == "ProductV2Dto")
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["id"] = new Microsoft.OpenApi.Any.OpenApiString("prod-001"),
                ["name"] = new Microsoft.OpenApi.Any.OpenApiString("Wireless Headphones"),
                ["description"] = new Microsoft.OpenApi.Any.OpenApiString("High-quality wireless headphones with noise cancellation"),
                ["price"] = new Microsoft.OpenApi.Any.OpenApiDouble(199.99),
                ["category"] = new Microsoft.OpenApi.Any.OpenApiString("Electronics"),
                ["isActive"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true),
                ["stockQuantity"] = new Microsoft.OpenApi.Any.OpenApiInteger(25),
                ["inStock"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true),
                ["warehouseLocation"] = new Microsoft.OpenApi.Any.OpenApiString("Warehouse-A-01"),
                ["supplier"] = new Microsoft.OpenApi.Any.OpenApiString("TechCorp Ltd"),
                ["tags"] = new Microsoft.OpenApi.Any.OpenApiArray
                {
                    new Microsoft.OpenApi.Any.OpenApiString("wireless"),
                    new Microsoft.OpenApi.Any.OpenApiString("bluetooth"),
                    new Microsoft.OpenApi.Any.OpenApiString("noise-cancelling")
                },
                ["metadata"] = new Microsoft.OpenApi.Any.OpenApiObject
                {
                    ["brand"] = new Microsoft.OpenApi.Any.OpenApiString("TechBrand"),
                    ["warranty"] = new Microsoft.OpenApi.Any.OpenApiString("2 years"),
                    ["color"] = new Microsoft.OpenApi.Any.OpenApiString("Black")
                }
            };
        }
        else if (context.Type.Name == "CreateProductRequest")
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["name"] = new Microsoft.OpenApi.Any.OpenApiString("New Product"),
                ["description"] = new Microsoft.OpenApi.Any.OpenApiString("Description of the new product"),
                ["price"] = new Microsoft.OpenApi.Any.OpenApiDouble(99.99),
                ["category"] = new Microsoft.OpenApi.Any.OpenApiString("Electronics"),
                ["isActive"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true)
            };
        }
        else if (context.Type.Name == "UpdateProductRequest")
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["name"] = new Microsoft.OpenApi.Any.OpenApiString("Updated Product Name"),
                ["price"] = new Microsoft.OpenApi.Any.OpenApiDouble(149.99),
                ["description"] = new Microsoft.OpenApi.Any.OpenApiString("Updated description"),
                ["category"] = new Microsoft.OpenApi.Any.OpenApiString("Updated Category"),
                ["isActive"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true)
            };
        }
        else if (context.Type.Name == "ErrorResponse")
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["type"] = new Microsoft.OpenApi.Any.OpenApiString("validation_error"),
                ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Validation Error"),
                ["detail"] = new Microsoft.OpenApi.Any.OpenApiString("One or more validation errors occurred"),
                ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(400),
                ["traceId"] = new Microsoft.OpenApi.Any.OpenApiString("0HN7KOKV3QR5V:00000001")
            };
        }
    }
}