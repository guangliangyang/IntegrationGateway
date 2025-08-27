namespace IntegrationGateway.Application.Common.Interfaces;

/// <summary>
/// Service to get current authenticated user information
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's unique identifier
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the current user's name/username
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Gets the current user's email
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets the current user's roles
    /// </summary>
    IEnumerable<string> Roles { get; }

    /// <summary>
    /// Gets whether the current user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the current user's claims
    /// </summary>
    IDictionary<string, string> Claims { get; }
}