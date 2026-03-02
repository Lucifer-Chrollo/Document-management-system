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
    private readonly DocumentManagementSystem.Services.IEncryptionService _encryptionService;

    public AccountController(
        SignInManager<ApplicationUser> signInManager, 
        UserManager<ApplicationUser> userManager, 
        ILogger<AccountController> logger,
        DocumentManagementSystem.Services.IEncryptionService encryptionService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _encryptionService = encryptionService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password, [FromForm] string returnUrl = "/")
    {
        var result = await _signInManager.PasswordSignInAsync(username, password, isPersistent: true, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            // Backfill encrypted password for legacy users on next login
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user != null && string.IsNullOrEmpty(user.EncryptedPassword))
                {
                    user.EncryptedPassword = await _encryptionService.EncryptTextAsync(password);
                    await _userManager.UpdateAsync(user);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backfill encrypted password for {User}", username);
            }

            return LocalRedirect(returnUrl);
        }
        
        return Redirect($"/login?error=Invalid login attempt&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromForm] string username, [FromForm] string email, [FromForm] string password)
    {

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
            // Store encrypted copy of password for admin viewing
            try
            {
                user.EncryptedPassword = await _encryptionService.EncryptTextAsync(password);
                await _userManager.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store encrypted password for user {User}", username);
            }

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

}
