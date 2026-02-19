using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DocumentManagementSystem.Models;
using System.Security.Claims;

namespace DocumentManagementSystem.Controllers;

[Route("account")]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password, [FromForm] string returnUrl = "/")
    {
        var result = await _signInManager.PasswordSignInAsync(username, password, isPersistent: true, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl);
        }
        
        return Redirect($"/login?error=Invalid login attempt&returnUrl={Uri.EscapeDataString(returnUrl)}");
        return Redirect($"/login?error=Invalid login attempt&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromForm] string username, [FromForm] string email, [FromForm] string password)
    {
        // Enforce Email Providers (Gmail, Hotmail, Yahoo, AOL, Outlook)
        var allowedDomains = new[] { "gmail.com", "hotmail.com", "yahoo.com", "aol.com", "outlook.com" };
        var domain = email.Split('@').LastOrDefault()?.ToLower();
        
        if (string.IsNullOrEmpty(domain) || !allowedDomains.Contains(domain))
        {
             return Redirect($"/register?error=Please use a valid email provider (Gmail, Hotmail, Yahoo, AOL, Outlook)");
        }

        var user = new ApplicationUser
        {
            UserName = username,
            Email = email,
            NormalizedUserName = username.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true, // Auto-confirm for demo
            IsActive = true,
            Department = "IT" // Default
        };

        var result = await _userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect("/");
        }

        return Redirect($"/register?error={Uri.EscapeDataString(result.Errors.FirstOrDefault()?.Description ?? "Registration failed")}");
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/login");
    }

    [HttpPost("loginexternal")]
    [AllowAnonymous]
    public IActionResult LoginExternal([FromForm] string provider, [FromForm] string returnUrl = "/")
    {
        // Request a redirect to the external login provider.
        var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet("ExternalLoginCallback")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "/", string? remoteError = null)
    {
        if (remoteError != null)
        {
            _logger.LogError("Error from external provider: {Error}", remoteError);
            return Redirect($"/login?error=Error from external provider: {remoteError}");
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogError("Error loading external login information.");
            return Redirect("/login?error=Error loading external login information.");
        }

        // Sign in the user with this external login provider if the user already has a login.
        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
        if (result.Succeeded)
        {
            _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity?.Name, info.LoginProvider);
            return LocalRedirect(returnUrl);
        }

        // If the user does not have an account, then create one
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var name = info.Principal.FindFirstValue(ClaimTypes.Name) ?? "Google User";
        
        if (email != null)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Create new user
                var allowedDomains = new[] { "gmail.com", "hotmail.com", "yahoo.com", "aol.com", "outlook.com" };
                var domain = email.Split('@').LastOrDefault()?.ToLower();
        
                if (string.IsNullOrEmpty(domain) || !allowedDomains.Contains(domain))
                {
                     return Redirect($"/login?error=Google account ({email}) is not from an allowed provider.");
                }

                user = new ApplicationUser
                {
                    UserName = email, // Use email as username
                    Email = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    NormalizedEmail = email.ToUpperInvariant(),
                    FirstName = name,
                    LastName = "",
                    EmailConfirmed = true,
                    IsActive = true,
                    Department = "Sales" // Default
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    _logger.LogError("Failed to create user: {Errors}", string.Join(",", createResult.Errors.Select(e => e.Description)));
                    return Redirect($"/login?error=Failed to create local account for {email}");
                }
            }

            // Link the external login
            var linkResult = await _userManager.AddLoginAsync(user, info);
            if (linkResult.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: true);
                return LocalRedirect(returnUrl);
            }
        }

        return Redirect("/login?error=Could not sign in with Google.");
    }
}
