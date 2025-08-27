using System.Security.Claims;
using IntegrationGateway.Application.Common.Interfaces;

namespace IntegrationGateway.Api.Services;

/// <summary>
/// Service implementation to get current authenticated user information from HttpContext
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ClaimsPrincipal? _user;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _user = _httpContextAccessor.HttpContext?.User;
    }

    public string? UserId => _user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                           _user?.FindFirst("sub")?.Value ??
                           _user?.FindFirst("userId")?.Value;

    public string? UserName => _user?.FindFirst(ClaimTypes.Name)?.Value ??
                             _user?.FindFirst("name")?.Value ??
                             _user?.FindFirst("username")?.Value;

    public string? Email => _user?.FindFirst(ClaimTypes.Email)?.Value ??
                          _user?.FindFirst("email")?.Value;

    public IEnumerable<string> Roles => _user?.FindAll(ClaimTypes.Role)?.Select(c => c.Value) ??
                                      _user?.FindAll("role")?.Select(c => c.Value) ??
                                      Enumerable.Empty<string>();

    public bool IsAuthenticated => _user?.Identity?.IsAuthenticated ?? false;

    public IDictionary<string, string> Claims => _user?.Claims?.ToDictionary(c => c.Type, c => c.Value) ??
                                               new Dictionary<string, string>();
}