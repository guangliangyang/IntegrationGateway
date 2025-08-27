using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behavior for handling unhandled exceptions with structured logging
/// </summary>
public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<UnhandledExceptionBehaviour<TRequest, TResponse>> _logger;

    public UnhandledExceptionBehaviour(ILogger<UnhandledExceptionBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;

            // Log the exception with full context
            _logger.LogError(ex, "Unhandled exception occurred while processing request: {RequestName} - {@Request}", 
                requestName, request);

            // Re-throw to allow higher-level exception handling
            throw;
        }
    }
}

/// <summary>
/// Application-specific exceptions for different scenarios
/// </summary>
public abstract class ApplicationException : Exception
{
    protected ApplicationException(string message) : base(message) { }
    protected ApplicationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a requested resource is not found
/// </summary>
public class NotFoundException : ApplicationException
{
    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }

    public NotFoundException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when a business rule validation fails
/// </summary>
public class BusinessRuleViolationException : ApplicationException
{
    public BusinessRuleViolationException(string message) : base(message) { }

    public BusinessRuleViolationException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when an external service is unavailable or fails
/// </summary>
public class ExternalServiceException : ApplicationException
{
    public string ServiceName { get; }

    public ExternalServiceException(string serviceName, string message) 
        : base($"External service '{serviceName}' error: {message}")
    {
        ServiceName = serviceName;
    }

    public ExternalServiceException(string serviceName, string message, Exception innerException)
        : base($"External service '{serviceName}' error: {message}", innerException)
    {
        ServiceName = serviceName;
    }
}