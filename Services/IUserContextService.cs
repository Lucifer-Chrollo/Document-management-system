namespace DocumentManagementSystem.Services;

/// <summary>
/// Abstracts user identity resolution so DMS services work with both
/// ASP.NET Identity (standalone) and legacy SessionManager (iBusinessFlex).
/// Swap the DI registration to switch auth backend.
/// </summary>
public interface IUserContextService
{
    /// <summary>Returns the current authenticated user's integer ID, or 0 if unauthenticated.</summary>
    int GetCurrentUserId();

    /// <summary>Returns the current user's display name, or "System" if unauthenticated.</summary>
    string GetCurrentUserName();

    /// <summary>Returns the remote IP address of the current request, or null.</summary>
    string? GetIpAddress();

    /// <summary>Returns the User-Agent header of the current request, or null.</summary>
    string? GetUserAgent();
}
