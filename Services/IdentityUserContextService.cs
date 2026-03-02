using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Default implementation — resolves user identity from ASP.NET Identity claims.
/// For legacy iBusinessFlex integration, replace this DI registration with
/// a LegacyUserContextService that falls back to SessionManager.
/// </summary>
public class IdentityUserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IdentityUserContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return 0;
        var claim = user.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
    }

    public string GetCurrentUserName()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
    }

    public string? GetIpAddress()
    {
        return _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    public string? GetUserAgent()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();
    }
}
