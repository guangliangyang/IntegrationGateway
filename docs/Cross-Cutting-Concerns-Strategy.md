# Cross-Cutting Concerns Strategy: MediatR Pipeline Behaviors vs ASP.NET Core Middleware

## Design Philosophy

This document outlines the strategic approach to handling cross-cutting concerns in the Integration Gateway using two complementary technologies: **ASP.NET Core Middleware** for HTTP-level concerns and **MediatR Pipeline Behaviors** for business-level concerns.

The core principle is **layered separation of responsibilities**, ensuring each technology handles concerns at its appropriate abstraction level while maintaining clean architecture principles.

## Technology Comparison

| Aspect | MediatR Pipeline Behaviors | ASP.NET Core Middleware |
|--------|---------------------------|------------------------|
| **Scope** | Business request processing | HTTP request processing |
| **Type Safety** | ✅ Strongly typed, compile-time checks | ❌ Runtime processing only |
| **Context Awareness** | ✅ Knows specific request types | ❌ Only HTTP context available |
| **Conditional Execution** | ✅ Based on request type/attributes | ❌ Applies to all requests |
| **Performance** | ❌ All behaviors execute per request | ✅ Can short-circuit early |
| **Debugging** | ❌ More complex debugging | ✅ Standard HTTP pipeline debugging |
| **Framework Integration** | ❌ Requires MediatR dependency | ✅ Native ASP.NET Core |
| **Request Lifecycle** | Business logic phase | HTTP processing phase |

## Usage Strategy

### ASP.NET Core Middleware - For HTTP-Level Concerns

**Use middleware when:**
- Processing ALL HTTP requests uniformly
- Handling framework-level concerns
- Early request pipeline intervention needed
- Standard HTTP operations required

**Current Implementation:**
```csharp
// Program.cs - HTTP-level pipeline
app.UseHttpsRedirection();
app.UseConfiguredRateLimiting();     // Rate limiting for all requests
app.UseConfiguredCors();             // CORS for all origins
app.UseAuthentication();             // JWT validation for all endpoints  
app.UseAuthorization();              // Authorization for protected endpoints
app.UseConfiguredIdempotency();      // Idempotency for write operations
```

**Appropriate for:**
- ✅ Authentication & Authorization
- ✅ Rate Limiting & Throttling
- ✅ CORS Configuration
- ✅ Global Exception Handling
- ✅ Request/Response Logging
- ✅ Security Headers
- ✅ Request Size Limits

### MediatR Pipeline Behaviors - For Business-Level Concerns

**Use pipeline behaviors when:**
- Processing specific business requests
- Type-aware processing required
- Conditional execution based on request attributes
- Business logic pipeline integration needed

**Current Implementation:**
```csharp
// DependencyInjection.cs - Business-level pipeline
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehaviour<,>));
```

**Appropriate for:**
- ✅ Business Validation (FluentValidation)
- ✅ Request-Specific Caching (`[Cacheable]` attribute)
- ✅ Performance Monitoring (business operations)
- ✅ Structured Business Logging
- ✅ Request/Response Transformation
- ✅ Business Rule Enforcement

## Implementation Examples

### Middleware Example: Rate Limiting
```csharp
// Applies to ALL HTTP requests uniformly
public class RateLimitingMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Check rate limits for all incoming requests
        if (!await _rateLimiter.TryAcquireAsync())
        {
            context.Response.StatusCode = 429;
            return;
        }
        await next(context);
    }
}
```

### Pipeline Behavior Example: Caching
```csharp
// Applies only to requests marked with [Cacheable] attribute
public class CachingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        var cacheAttribute = typeof(TRequest).GetCustomAttribute<CacheableAttribute>();
        if (cacheAttribute == null) return await next(); // Skip non-cacheable requests
        
        // Type-aware caching logic
        var cacheKey = GenerateCacheKey(request, cacheAttribute);
        // ... caching implementation
    }
}
```

## Decision Guidelines

### Choose Middleware When:
1. **Universal Application**: Need to process ALL HTTP requests
2. **HTTP Protocol Concerns**: Dealing with HTTP headers, status codes, etc.
3. **Framework Integration**: Leveraging ASP.NET Core features
4. **Early Intervention**: Need to handle requests before reaching controllers
5. **Performance Critical**: Require early short-circuiting capabilities

### Choose Pipeline Behaviors When:
1. **Business Logic Integration**: Processing business requests/commands/queries
2. **Type-Specific Logic**: Different handling based on request types
3. **Conditional Processing**: Only certain requests need processing
4. **Request Context**: Need access to strongly-typed request data
5. **Business Pipeline**: Part of the business logic flow

## Current Project Architecture

```
HTTP Request
    ↓
┌─────────────────────────┐
│   ASP.NET Middleware    │  ← HTTP-level concerns
│  • Authentication       │
│  • Rate Limiting        │
│  • CORS                 │
│  • Idempotency          │
└─────────────────────────┘
    ↓
┌─────────────────────────┐
│      Controller         │
└─────────────────────────┘
    ↓
┌─────────────────────────┐
│ MediatR Pipeline        │  ← Business-level concerns
│  • Validation           │
│  • Performance          │
│  • Caching              │
│  • Logging              │
└─────────────────────────┘
    ↓
┌─────────────────────────┐
│    Request Handler      │  ← Business logic
└─────────────────────────┘
```

## Best Practices

1. **Layered Responsibility**: Keep HTTP concerns in middleware, business concerns in pipeline behaviors
2. **Performance Consideration**: Use middleware for operations that might short-circuit the pipeline
3. **Type Safety**: Leverage pipeline behaviors for strongly-typed request processing
4. **Debugging**: Use middleware for easier HTTP-level debugging, behaviors for business-level tracing
5. **Configuration**: Prefer configuration-driven middleware for operational concerns

## Conclusion

Both technologies serve essential roles in a well-architected system:

- **Middleware** provides the foundation for HTTP-level cross-cutting concerns
- **Pipeline Behaviors** enable sophisticated business-level request processing

The combination creates a robust, maintainable, and performant request processing pipeline that separates HTTP infrastructure concerns from business logic concerns, following clean architecture principles.

---

*This strategy ensures optimal separation of concerns while maximizing the strengths of both technologies in enterprise integration scenarios.*