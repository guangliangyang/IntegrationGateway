using System.Text;
using System.Text.Json;
using IntegrationGateway.Models.Common;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Api.Middleware;

/// <summary>
/// High-concurrency idempotency middleware with per-operation locking
/// Provides enterprise-grade idempotency guarantees for write operations
/// </summary>
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
            await WriteErrorResponse(context, 400, "missing_idempotency_key", 
                "Idempotency-Key header is required for POST and PUT requests");
            return;
        }

        var idempotencyKey = idempotencyKeyValues.First()!;
        
        // Validate idempotency key format
        if (idempotencyKey.Length < 16 || idempotencyKey.Length > 128)
        {
            await WriteErrorResponse(context, 400, "invalid_idempotency_key",
                "Idempotency-Key header must be between 16 and 128 characters");
            return;
        }

        try
        {
            // Read and hash the request body
            context.Request.EnableBuffering();
            var bodyContent = await ReadRequestBodyAsync(context.Request);
            var bodyHash = IdempotencyKey.GenerateBodyHash(bodyContent);
            var operation = $"{context.Request.Method}_{context.Request.Path}";

            _logger.LogDebug("Processing idempotent request: {Key}, Operation: {Operation}", 
                idempotencyKey, operation);

            // Use high-concurrency safe method to check/create operation
            var (isExisting, operationRecord) = await idempotencyService.GetOrCreateOperationAsync(
                idempotencyKey, operation, bodyHash, context.RequestAborted);

            if (isExisting && !string.IsNullOrEmpty(operationRecord.ResponseBody))
            {
                // Return cached response for duplicate request
                _logger.LogInformation("Returning cached response for duplicate idempotent request: {Key}", 
                    idempotencyKey);
                
                context.Response.StatusCode = operationRecord.ResponseStatusCode ?? 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(operationRecord.ResponseBody);
                return;
            }

            if (isExisting && string.IsNullOrEmpty(operationRecord.ResponseBody))
            {
                // Another request with same idempotency key is still processing
                _logger.LogInformation("Concurrent request detected, returning 409 Conflict: {Key}", 
                    idempotencyKey);
                
                await WriteErrorResponse(context, 409, "concurrent_request",
                    "A request with the same idempotency key is currently being processed");
                return;
            }

            // Reset request body position for downstream processing
            context.Request.Body.Position = 0;

            // Store context for controllers
            context.Items["IdempotencyKey"] = idempotencyKey;
            context.Items["IdempotencyOperation"] = operation;
            context.Items["IdempotencyBodyHash"] = bodyHash;
            context.Items["IsNewIdempotentOperation"] = true;

            // Capture the response
            var originalBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            try
            {
                // Continue to next middleware/controller
                await _next(context);

                // Capture and cache the response
                responseBodyStream.Position = 0;
                var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
                
                // Update the operation with response data
                await idempotencyService.UpdateOperationResponseAsync(
                    idempotencyKey, operation, bodyHash, responseBody, context.Response.StatusCode);

                // Write response to original stream
                responseBodyStream.Position = 0;
                await responseBodyStream.CopyToAsync(originalBodyStream);

                _logger.LogInformation("Cached response for idempotent operation: {Key}, Status: {Status}", 
                    idempotencyKey, context.Response.StatusCode);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Idempotent request cancelled: {Key}", idempotencyKey);
            // Let cancellation bubble up
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EnhancedIdempotencyMiddleware for key: {Key}", idempotencyKey);
            
            if (!context.Response.HasStarted)
            {
                await WriteErrorResponse(context, 500, "internal_error", 
                    "An unexpected error occurred while processing the request");
            }
        }
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private static async Task WriteErrorResponse(HttpContext context, int statusCode, string errorType, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        
        var errorResponse = new
        {
            type = errorType,
            title = GetStatusTitle(statusCode),
            detail = message,
            status = statusCode,
            traceId = context.TraceIdentifier
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    private static string GetStatusTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        409 => "Conflict", 
        500 => "Internal Server Error",
        _ => "Error"
    };
}