using System.Reflection;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behavior for handling authorization requirements
/// </summary>
public class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<AuthorizationBehaviour<TRequest, TResponse>> _logger;

    public AuthorizationBehaviour(ILogger<AuthorizationBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var authorizeAttributes = typeof(TRequest).GetCustomAttributes<AuthorizeAttribute>();

        if (authorizeAttributes.Any())
        {
            // For now, this is a placeholder for authorization logic
            // In a real application, you would:
            // 1. Get the current user from ICurrentUserService
            // 2. Check permissions based on the authorize attributes
            // 3. Throw UnauthorizedAccessException if not authorized

            var requestName = typeof(TRequest).Name;
            var requiredPermissions = authorizeAttributes.SelectMany(a => a.Permissions);

            _logger.LogDebug("Authorization check for {RequestName} with permissions: {@Permissions}", 
                requestName, requiredPermissions);

            // TODO: Implement actual authorization logic here
            // This is where you would integrate with your authentication system
        }

        return await next();
    }
}

/// <summary>
/// Attribute to mark requests that require authorization
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AuthorizeAttribute : Attribute
{
    public AuthorizeAttribute() { }

    public AuthorizeAttribute(params string[] permissions)
    {
        Permissions = permissions;
    }

    public IEnumerable<string> Permissions { get; } = Array.Empty<string>();
}

/// <summary>
/// Exception thrown when a user is not authorized to perform an operation
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() : base("Access to this resource is forbidden.") { }

    public ForbiddenAccessException(string message) : base(message) { }
}