using System.Reflection;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behavior for structured logging of requests and responses
/// </summary>
public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestGuid = Guid.NewGuid().ToString();

        // Log request start
        _logger.LogInformation("Handling request: {RequestName} ({RequestId})", 
            requestName, requestGuid);

        // Log request details (without sensitive information)
        var requestDetails = GetSafeRequestDetails(request);
        if (!string.IsNullOrEmpty(requestDetails))
        {
            _logger.LogDebug("Request details for {RequestName} ({RequestId}): {@RequestDetails}", 
                requestName, requestGuid, requestDetails);
        }

        try
        {
            var response = await next();

            // Log successful completion
            _logger.LogInformation("Completed request: {RequestName} ({RequestId})", 
                requestName, requestGuid);

            return response;
        }
        catch (Exception)
        {
            // Note: Exception details will be logged by UnhandledExceptionBehaviour
            // No logging here to avoid duplication
            throw;
        }
    }

    /// <summary>
    /// Extract safe request details for logging (excludes sensitive information)
    /// </summary>
    private static string GetSafeRequestDetails(TRequest request)
    {
        try
        {
            var properties = typeof(TRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var safeProperties = new Dictionary<string, object?>();

            foreach (var prop in properties)
            {
                if (IsSensitiveProperty(prop.Name))
                    continue;

                var value = prop.GetValue(request);
                if (value != null)
                {
                    // Truncate long strings
                    if (value is string strValue && strValue.Length > 500)
                    {
                        safeProperties[prop.Name] = strValue[..500] + "...";
                    }
                    else
                    {
                        safeProperties[prop.Name] = value;
                    }
                }
            }

            return safeProperties.Any() ? 
                string.Join(", ", safeProperties.Select(kvp => $"{kvp.Key}: {kvp.Value}")) : 
                string.Empty;
        }
        catch
        {
            return "Unable to extract request details safely";
        }
    }

    /// <summary>
    /// Check if a property contains sensitive information that should not be logged
    /// </summary>
    private static bool IsSensitiveProperty(string propertyName)
    {
        var sensitiveKeywords = new[]
        {
            "password", "pwd", "secret", "key", "token", "auth", "credential",
            "ssn", "social", "credit", "card", "cvv", "pin"
        };

        return sensitiveKeywords.Any(keyword => 
            propertyName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}