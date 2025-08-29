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

// Note: Custom exception types removed as they were not being used anywhere in the codebase.
// If needed in the future, specific exceptions should be defined when actually implemented.