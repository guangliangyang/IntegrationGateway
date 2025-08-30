namespace IntegrationGateway.Models.Exceptions;

/// <summary>
/// Base exception class for all application-specific exceptions
/// </summary>
public abstract class BaseApplicationException : Exception
{
    protected BaseApplicationException(string message) : base(message) { }
    
    protected BaseApplicationException(string message, Exception innerException) : base(message, innerException) { }
    
    /// <summary>
    /// HTTP status code that should be returned for this exception
    /// </summary>
    public abstract int StatusCode { get; }
    
    /// <summary>
    /// Error type identifier for client applications
    /// </summary>
    public abstract string ErrorType { get; }
}

/// <summary>
/// Exception for validation errors (400 Bad Request)
/// </summary>
public class ValidationException : BaseApplicationException
{
    public override int StatusCode => 400;
    public override string ErrorType => "validation_error";
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors) : base("One or more validation failures occurred.")
    {
        Errors = errors;
    }
}

/// <summary>
/// Exception for authentication failures (401 Unauthorized)
/// </summary>
public class UnauthorizedException : BaseApplicationException
{
    public override int StatusCode => 401;
    public override string ErrorType => "unauthorized";

    public UnauthorizedException(string message = "Authentication failed") : base(message) { }
}

/// <summary>
/// Exception for authorization failures (403 Forbidden)
/// </summary>
public class ForbiddenException : BaseApplicationException
{
    public override int StatusCode => 403;
    public override string ErrorType => "forbidden";

    public ForbiddenException(string message = "Access denied") : base(message) { }
}

/// <summary>
/// Exception for resource not found (404 Not Found)
/// </summary>
public class NotFoundException : BaseApplicationException
{
    public override int StatusCode => 404;
    public override string ErrorType => "not_found";

    public NotFoundException(string message) : base(message) { }
    
    public NotFoundException(string name, object key) : base($"Entity \"{name}\" ({key}) was not found.") { }
}

/// <summary>
/// Exception for conflicts (409 Conflict)
/// </summary>
public class ConflictException : BaseApplicationException
{
    public override int StatusCode => 409;
    public override string ErrorType => "conflict";

    public ConflictException(string message) : base(message) { }
}

/// <summary>
/// Exception for business rule violations (422 Unprocessable Entity)
/// </summary>
public class BusinessRuleViolationException : BaseApplicationException
{
    public override int StatusCode => 422;
    public override string ErrorType => "business_rule_violation";

    public BusinessRuleViolationException(string message) : base(message) { }
}

/// <summary>
/// Exception for external service failures (502 Bad Gateway)
/// </summary>
public class ExternalServiceException : BaseApplicationException
{
    public override int StatusCode => 502;
    public override string ErrorType => "external_service_error";
    public string ServiceName { get; }

    public ExternalServiceException(string serviceName, string message) : base(message)
    {
        ServiceName = serviceName;
    }
    
    public ExternalServiceException(string serviceName, string message, Exception innerException) : base(message, innerException)
    {
        ServiceName = serviceName;
    }
}

/// <summary>
/// Exception for service unavailable (503 Service Unavailable)
/// </summary>
public class ServiceUnavailableException : BaseApplicationException
{
    public override int StatusCode => 503;
    public override string ErrorType => "service_unavailable";

    public ServiceUnavailableException(string message = "Service is temporarily unavailable") : base(message) { }
}