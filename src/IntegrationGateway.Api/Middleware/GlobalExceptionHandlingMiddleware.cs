using System.Diagnostics;
using System.Net;
using System.Text.Json;
using IntegrationGateway.Models.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationGateway.Api.Middleware;

/// <summary>
/// Global exception handling middleware that converts exceptions to appropriate HTTP responses
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = CreateErrorResponse(exception);
        
        // Log the exception with appropriate level
        LogException(exception, response.Status ?? 500);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.Status ?? 500;

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    private ProblemDetails CreateErrorResponse(Exception exception)
    {
        var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString();

        return exception switch
        {
            BaseApplicationException appEx => new ProblemDetails
            {
                Type = $"https://httpstatuses.com/{appEx.StatusCode}",
                Title = GetTitleForStatusCode(appEx.StatusCode),
                Status = appEx.StatusCode,
                Detail = GetSafeErrorMessage(appEx),
                Instance = Activity.Current?.Id,
                Extensions = new Dictionary<string, object?>
                {
                    ["errorType"] = appEx.ErrorType,
                    ["traceId"] = traceId,
                    ["timestamp"] = DateTime.UtcNow,
                    ["errors"] = GetValidationErrors(appEx),
                    ["innerException"] = _environment.IsDevelopment() ? exception.InnerException?.Message : null,
                    ["stackTrace"] = _environment.IsDevelopment() ? exception.StackTrace : null
                }
            },
            
            TaskCanceledException => new ProblemDetails
            {
                Type = "https://httpstatuses.com/408",
                Title = "Request Timeout",
                Status = 408,
                Detail = "The request timed out",
                Instance = Activity.Current?.Id,
                Extensions = new Dictionary<string, object?>
                {
                    ["errorType"] = "request_timeout",
                    ["traceId"] = traceId,
                    ["timestamp"] = DateTime.UtcNow
                }
            },
            
            ArgumentException => new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = 400,
                Detail = _environment.IsDevelopment() ? exception.Message : "Invalid request parameters",
                Instance = Activity.Current?.Id,
                Extensions = new Dictionary<string, object?>
                {
                    ["errorType"] = "bad_request",
                    ["traceId"] = traceId,
                    ["timestamp"] = DateTime.UtcNow
                }
            },
            
            _ => new ProblemDetails
            {
                Type = "https://httpstatuses.com/500",
                Title = "Internal Server Error",
                Status = 500,
                Detail = _environment.IsDevelopment() ? exception.Message : "An unexpected error occurred",
                Instance = Activity.Current?.Id,
                Extensions = new Dictionary<string, object?>
                {
                    ["errorType"] = "internal_server_error",
                    ["traceId"] = traceId,
                    ["timestamp"] = DateTime.UtcNow,
                    ["stackTrace"] = _environment.IsDevelopment() ? exception.StackTrace : null
                }
            }
        };
    }

    private static string GetTitleForStatusCode(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        408 => "Request Timeout",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        _ => "Error"
    };

    private static object? GetValidationErrors(BaseApplicationException exception)
    {
        return exception is ValidationException validationEx ? validationEx.Errors : null;
    }

    private string GetSafeErrorMessage(BaseApplicationException exception)
    {
        // For business exceptions, always return the message (they're designed to be user-safe)
        if (exception is ValidationException || 
            exception is NotFoundException || 
            exception is ConflictException || 
            exception is BusinessRuleViolationException)
        {
            return exception.Message;
        }

        // For security/auth exceptions, return generic messages in production
        if (exception is UnauthorizedException)
        {
            return _environment.IsDevelopment() ? exception.Message : "Authentication required";
        }

        if (exception is ForbiddenException)
        {
            return _environment.IsDevelopment() ? exception.Message : "Access denied";
        }

        // For external service exceptions, be more cautious in production
        if (exception is ExternalServiceException extEx)
        {
            return _environment.IsDevelopment() 
                ? $"External service '{extEx.ServiceName}' error: {exception.Message}"
                : "External service temporarily unavailable";
        }

        // For service unavailable, return generic message
        if (exception is ServiceUnavailableException)
        {
            return _environment.IsDevelopment() ? exception.Message : "Service temporarily unavailable";
        }

        // Default: return the message as-is (should not happen with proper exception design)
        return exception.Message;
    }

    private void LogException(Exception exception, int statusCode)
    {
        var logLevel = statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, exception, 
            "HTTP {StatusCode} - {ExceptionType}: {Message}",
            statusCode, exception.GetType().Name, exception.Message);
    }
}

/// <summary>
/// Extension methods for registering the global exception handling middleware
/// </summary>
public static class GlobalExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}