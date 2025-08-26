using System.Text;
using IntegrationGateway.Models.Common;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyService idempotencyService)
    {
        // Only process POST and PUT requests
        if (context.Request.Method != HttpMethods.Post && context.Request.Method != HttpMethods.Put)
        {
            await _next(context);
            return;
        }

        // Check for Idempotency-Key header
        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues) ||
            string.IsNullOrWhiteSpace(idempotencyKeyValues.FirstOrDefault()))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Idempotency-Key header is required for POST and PUT requests");
            return;
        }

        var idempotencyKey = idempotencyKeyValues.First()!;
        
        // Validate idempotency key format (should be a valid GUID or similar)
        if (idempotencyKey.Length < 16 || idempotencyKey.Length > 128)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Idempotency-Key header must be between 16 and 128 characters");
            return;
        }

        try
        {
            // Read the request body
            context.Request.EnableBuffering();
            var bodyContent = await ReadRequestBodyAsync(context.Request);
            
            // Generate body hash
            var bodyHash = IdempotencyKey.GenerateBodyHash(bodyContent);
            var operation = $"{context.Request.Method}_{context.Request.Path}";

            // Check if we've seen this operation before
            var existingOperation = await idempotencyService.GetAsync(idempotencyKey, operation, bodyHash);
            
            if (existingOperation != null && !string.IsNullOrEmpty(existingOperation.ResponseBody))
            {
                // Return the cached response
                _logger.LogInformation("Returning cached response for idempotent operation: {IdempotencyKey}", idempotencyKey);
                
                context.Response.StatusCode = existingOperation.ResponseStatusCode ?? 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(existingOperation.ResponseBody);
                return;
            }

            // Reset the request body position for downstream processing
            context.Request.Body.Position = 0;

            // Store the idempotency key for controllers to use
            context.Items["IdempotencyKey"] = idempotencyKey;
            context.Items["IdempotencyOperation"] = operation;
            context.Items["IdempotencyBodyHash"] = bodyHash;

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in IdempotencyMiddleware for key: {IdempotencyKey}", idempotencyKey);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error");
        }
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }
}