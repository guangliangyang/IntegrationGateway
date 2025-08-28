# API Multi-Versioning Technical Implementation Guide

## Overview

This document explains the technical approach for implementing API multi-versioning with backward compatibility in the Integration Gateway API. Our solution uses an **inheritance-based controller design** combined with **versioned MediatR handlers** to achieve 100% backward compatibility while enabling enhanced functionality in newer versions.

## Architecture Philosophy

### Core Principles

1. **Backward Compatibility First**: New versions must never break existing client integrations
2. **Inheritance-Based Design**: V2+ controllers inherit from V1 to ensure all V1 functionality remains available
3. **Progressive Enhancement**: Each version adds capabilities without removing existing features
4. **Clean Separation**: Version-specific logic is separated through dedicated handlers and DTOs

### Key Benefits

- **Zero Breaking Changes**: V1 clients continue working unchanged when V2 is deployed
- **Code Reuse**: Shared logic through inheritance reduces duplication
- **Maintainability**: Clear separation of version-specific concerns
- **Scalability**: Pattern extends easily to V3, V4, etc.

## Technology Stack

### Core Dependencies

```xml
<!-- API Versioning -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Versioning" Version="5.1.0" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Versioning.ApiExplorer" Version="5.1.0" />

<!-- CQRS Pattern -->
<PackageReference Include="MediatR" Version="12.x" />

<!-- Documentation -->
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.7.3" />
<PackageReference Include="Swashbuckle.AspNetCore.Annotations" Version="6.7.3" />
```

### Configuration Setup

```csharp
// Program.cs - API Versioning Configuration
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),           // /api/v1/products
        new QueryStringApiVersionReader("version"), // ?version=1.0
        new HeaderApiVersionReader("X-API-Version") // X-API-Version: 1.0
    );
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

## Implementation Patterns

### 1. Controller Inheritance Pattern

**V1 Controller (Base Implementation)**:
```csharp
[ApiController]
[Route("api/v1/[controller]")]
[ApiVersion("1.0")]
public class ProductsController : ControllerBase
{
    protected readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public virtual async Task<ActionResult<ProductListResponse>> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductsV1Query(page, pageSize);
        var response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
    }

    // Other V1 methods marked as 'virtual' for overriding...
}
```

**V2 Controller (Enhanced Implementation)**:
```csharp
[ApiController]
[Route("api/v2/[controller]")]
[ApiVersion("2.0")]
public class ProductsController : V1.ProductsController  // Inheritance!
{
    public ProductsController(IMediator mediator) : base(mediator) { }

    // Override V1 method to return enhanced V2 response
    [HttpGet]
    public override async Task<ActionResult<ProductListResponse>> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductsV2Query(page, pageSize); // V2 Query!
        var response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
    }

    // V2-specific new endpoints
    [HttpPost("batch")]
    public async Task<ActionResult<List<ProductV2Dto>>> CreateProductsBatch(
        [FromBody] List<CreateProductRequest> requests,
        CancellationToken cancellationToken = default)
    {
        // V2-only functionality
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ProductHistoryDto>> GetProductHistory(
        string id, CancellationToken cancellationToken = default)
    {
        // V2-only functionality
    }
}
```

### 2. MediatR Handler Versioning Pattern

**Separate Query Handlers for Each Version**:

```csharp
// V1 Query Handler
[Cacheable(300)]
public record GetProductsV1Query(int Page = 1, int PageSize = 50) : IRequest<ProductListResponse>;

public class GetProductsQueryV1Handler : IRequestHandler<GetProductsV1Query, ProductListResponse>
{
    public async Task<ProductListResponse> Handle(GetProductsV1Query request, CancellationToken cancellationToken)
    {
        var result = await _productService.GetProductsAsync(request.Page, request.PageSize, cancellationToken);
        return result; // Returns ProductListResponse with ProductDto[]
    }
}

// V2 Query Handler
[Cacheable(300)]
public record GetProductsV2Query(int Page = 1, int PageSize = 50) : IRequest<ProductListV2Response>;

public class GetProductsV2QueryHandler : IRequestHandler<GetProductsV2Query, ProductListV2Response>
{
    public async Task<ProductListV2Response> Handle(GetProductsV2Query request, CancellationToken cancellationToken)
    {
        var result = await _productService.GetProductsV2Async(request.Page, request.PageSize, cancellationToken);
        return result; // Returns ProductListV2Response with ProductV2Dto[]
    }
}
```

### 3. DTO Extension Pattern

**Progressive DTO Enhancement**:

```csharp
// V1 Base DTO
public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int StockQuantity { get; set; }
    public bool InStock { get; set; }
    public string? WarehouseLocation { get; set; }
}

// V2 Extended DTO - Inherits ALL V1 fields
public class ProductV2Dto : ProductDto  // Inheritance ensures compatibility!
{
    // V2-specific enhancements
    public string? Supplier { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

// Response Models
public class ProductListResponse
{
    public List<ProductDto> Products { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class ProductListV2Response
{
    public List<ProductV2Dto> Products { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public Dictionary<string, object> Metadata { get; set; } = new(); // V2 enhancement
}
```

### 4. Service Layer Versioning

**Version-Specific Service Methods**:

```csharp
public interface IProductService
{
    // V1 Methods
    Task<ProductListResponse> GetProductsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductDto?> GetProductAsync(string productId, CancellationToken cancellationToken);
    
    // V2 Methods - Enhanced versions
    Task<ProductListV2Response> GetProductsV2Async(int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductV2Dto?> GetProductV2Async(string productId, CancellationToken cancellationToken);
    
    // Shared methods (same across versions)
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductDto> UpdateProductAsync(string productId, UpdateProductRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteProductAsync(string productId, CancellationToken cancellationToken);
}
```

## Swagger Multi-Version Documentation

### Separate OpenAPI Specifications

```csharp
public static class SwaggerConfiguration
{
    public static void ConfigureSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // V1 Documentation
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Integration Gateway API",
                Version = "v1.0",
                Description = "Stable API version with core product management features"
            });

            // V2 Documentation
            options.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "Integration Gateway API", 
                Version = "v2.0",
                Description = "Enhanced API with additional product information and batch operations"
            });

            // Custom operation filters
            options.OperationFilter<IdempotencyHeaderOperationFilter>();
            options.SchemaFilter<ExampleSchemaFilter>();
        });
    }
}
```

## Developer Guidelines for Adding New Versions

### Step-by-Step Process for Adding V3

1. **Create V3 Controller**:
```csharp
[ApiController]
[Route("api/v3/[controller]")]
[ApiVersion("3.0")]
public class ProductsController : V2.ProductsController  // Inherit from V2
{
    public ProductsController(IMediator mediator) : base(mediator) { }
    
    // Override methods that need V3-specific behavior
    // Add V3-only endpoints
}
```

2. **Create V3 DTOs**:
```csharp
public class ProductV3Dto : ProductV2Dto  // Extend V2 DTO
{
    // Add V3-specific fields
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public List<ProductReview> Reviews { get; set; } = new();
}
```

3. **Create V3 MediatR Handlers**:
```csharp
public record GetProductsV3Query(int Page = 1, int PageSize = 50) : IRequest<ProductListV3Response>;

public class GetProductsV3QueryHandler : IRequestHandler<GetProductsV3Query, ProductListV3Response>
{
    // V3-specific implementation
}
```

4. **Extend Service Interface**:
```csharp
public interface IProductService
{
    // Existing V1/V2 methods...
    
    // Add V3 methods
    Task<ProductListV3Response> GetProductsV3Async(int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductV3Dto?> GetProductV3Async(string productId, CancellationToken cancellationToken);
}
```

5. **Update Swagger Configuration**:
```csharp
options.SwaggerDoc("v3", new OpenApiInfo
{
    Title = "Integration Gateway API",
    Version = "v3.0", 
    Description = "Latest API with reviews and audit trail features"
});
```

6. **Update API Versioning Registration**:
```csharp
builder.Services.AddApiVersioning(options =>
{
    // Keep existing configuration
    options.DefaultApiVersion = new ApiVersion(3, 0); // Update default if desired
});
```

## Best Practices

### When to Create a New Version

✅ **DO create a new version when:**
- Adding new response fields that enhance existing data
- Adding new endpoints with additional functionality
- Changing response structure significantly (but maintaining core fields)
- Adding new optional request parameters

❌ **DON'T create a new version for:**
- Bug fixes
- Internal optimizations
- Adding optional fields to existing responses (if backward compatible)
- Security updates

### Backward Compatibility Rules

1. **Never remove fields** from existing response models
2. **Never change field types** in existing models
3. **Never make optional fields required** in existing requests
4. **Always make new request fields optional** with sensible defaults
5. **Keep HTTP status codes consistent** across versions

### Code Organization

```
Controllers/
├── V1/
│   └── ProductsController.cs      # Base implementation
├── V2/
│   └── ProductsController.cs      # Inherits from V1
└── V3/
    └── ProductsController.cs      # Inherits from V2

Application/Products/
├── Queries/
│   ├── GetProductsV1Query.cs
│   ├── GetProductsV2Query.cs
│   └── GetProductsV3Query.cs
└── Commands/
    └── CreateProductCommand.cs    # Shared across versions

Models/DTOs/
├── ProductDto.cs                  # V1 base model
├── ProductV2Dto.cs               # Extends ProductDto  
└── ProductV3Dto.cs               # Extends ProductV2Dto
```

## Testing Strategy

### Integration Tests

```csharp
[TestClass]
public class ProductsV2ControllerTests
{
    [TestMethod]
    public async Task GetProducts_V2_Returns_Enhanced_Response()
    {
        // Test that V2 returns all V1 fields plus V2 enhancements
        var response = await _client.GetAsync("/api/v2/products");
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ProductListV2Response>(content);
        
        // Verify V1 fields are present
        Assert.IsNotNull(result.Products[0].Name);
        Assert.IsNotNull(result.Products[0].Price);
        
        // Verify V2 enhancements are present
        Assert.IsNotNull(result.Products[0].Supplier);
        Assert.IsNotNull(result.Products[0].Tags);
        Assert.IsNotNull(result.Products[0].Metadata);
        Assert.IsNotNull(result.Metadata); // Response-level metadata
    }

    [TestMethod]
    public async Task V2_Controller_Inherits_V1_Delete_Behavior()
    {
        // Test that inherited methods work correctly
        var response = await _client.DeleteAsync("/api/v2/products/test-id");
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }
}
```

### Backward Compatibility Tests

```csharp
[TestClass]
public class BackwardCompatibilityTests
{
    [TestMethod]
    public async Task V1_Clients_Continue_Working_When_V2_Deployed()
    {
        // Simulate V1 client making requests
        var v1Response = await _client.GetAsync("/api/v1/products");
        var v1Content = await v1Response.Content.ReadAsStringAsync();
        
        // Verify V1 response structure unchanged
        var v1Result = JsonSerializer.Deserialize<ProductListResponse>(v1Content);
        Assert.IsInstanceOfType(v1Result.Products[0], typeof(ProductDto));
        
        // Verify no V2-specific fields in V1 response
        var json = JObject.Parse(v1Content);
        Assert.IsNull(json["products"]?[0]?["supplier"]);
        Assert.IsNull(json["products"]?[0]?["tags"]);
        Assert.IsNull(json["metadata"]);
    }
}
```

## Performance Considerations

### Caching Strategy

- **Version-Specific Cache Keys**: Separate cache entries for V1/V2 responses
- **Shared Data Layer**: Common data retrieval with version-specific projections
- **Cache Invalidation**: Coordinate invalidation across versions

### Query Optimization

```csharp
// V1 Query - Basic fields only
public async Task<ProductListResponse> GetProductsAsync(int page, int pageSize)
{
    return await _context.Products
        .Where(p => p.IsActive)
        .Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            // ... basic fields only
        })
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
}

// V2 Query - Enhanced fields with optimized joins
public async Task<ProductListV2Response> GetProductsV2Async(int page, int pageSize)
{
    return await _context.Products
        .Include(p => p.Supplier)
        .Include(p => p.Tags)
        .Where(p => p.IsActive)
        .Select(p => new ProductV2Dto
        {
            // All V1 fields
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            // ... V2 enhancements
            Supplier = p.Supplier.Name,
            Tags = p.Tags.Select(t => t.Name).ToList(),
            Metadata = p.CustomFields.ToDictionary(cf => cf.Key, cf => cf.Value)
        })
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
}
```

## Migration Path

### For API Consumers

1. **Start with V1**: Stable, well-tested baseline
2. **Evaluate V2 Benefits**: Review enhanced fields and new endpoints
3. **Gradual Migration**: Update clients incrementally
4. **Parallel Testing**: Test V2 alongside existing V1 integration

### For Developers

1. **Always start with latest version** for new features
2. **Backport critical fixes** to all supported versions if needed
3. **Monitor usage metrics** to understand version adoption
4. **Plan deprecation timeline** for very old versions (if ever needed)

## Monitoring and Observability

### Version Usage Tracking

```csharp
public class ApiVersionTelemetryMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var apiVersion = context.GetRequestedApiVersion()?.ToString() ?? "unknown";
        
        using var activity = ActivitySource.StartActivity("api_request");
        activity?.SetTag("api.version", apiVersion);
        activity?.SetTag("api.endpoint", context.Request.Path);
        
        await next(context);
    }
}
```

### Health Checks per Version

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("api-v1", () => HealthCheckResult.Healthy())
    .AddCheck("api-v2", () => HealthCheckResult.Healthy())
    .AddCheck("backward-compatibility", () => 
    {
        // Custom check to ensure V1 functionality in V2+
        return HealthCheckResult.Healthy();
    });
```

## Conclusion

This inheritance-based API versioning approach provides:

- **100% Backward Compatibility**: V1 clients never break
- **Progressive Enhancement**: New versions add value without complexity
- **Maintainable Code**: Clear separation with shared logic through inheritance
- **Scalable Pattern**: Easy to extend to V3, V4, and beyond

The key insight is using **controller inheritance** combined with **versioned MediatR handlers** and **extended DTOs** to achieve both backward compatibility and enhanced functionality. This pattern has proven effective in production environments where API stability is critical while continuous improvement is required.

---

*Created: January 2025*
*Version: 1.0*
*Author: Integration Gateway Team*