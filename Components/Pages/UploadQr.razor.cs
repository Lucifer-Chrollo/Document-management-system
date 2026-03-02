using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Radzen;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace DocumentManagementSystem.Components.Pages
{
    public partial class UploadQr
    {
private bool showQrCode;
    private bool isGenerating;
    private int expiryMinutes = 15;
    private int maxFiles = 10000;
    private int? selectedCategoryId;
    private string sessionLabel = "";
    private string uploadUrl = "";
    private string qrCodeUrl = "";
    private string generatedPin = "";
    private int currentUserId;
    
    private IEnumerable<UploadSession> sessions = Enumerable.Empty<UploadSession>();
    private IEnumerable<Category> categories = Enumerable.Empty<Category>();

    private readonly List<object> expiryOptions = new()
    {
        new { Text = "5 minutes", Value = 5 },
        new { Text = "15 minutes", Value = 15 },
        new { Text = "30 minutes", Value = 30 },
        new { Text = "1 hour", Value = 60 },
        new { Text = "2 hours", Value = 120 }
    };

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var uid))
        {
            currentUserId = uid;
        }

        await LoadCategories();
        await LoadSessions();
    }

    private async Task LoadCategories()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var isAdmin = user.Identity?.Name?.ToLower() == "admin" || user.IsInRole("Admin");

        if (isAdmin)
        {
            categories = await CategoryService.GetCategoriesAsync();
        }
        else
        {
            categories = await CategoryService.GetCategoriesByUserAsync(currentUserId);
        }
    }

    private async Task LoadSessions()
    {
        sessions = await UploadSessionService.GetUserSessionsAsync(currentUserId);
        StateHasChanged();
    }

    private async Task GenerateSession()
    {
        isGenerating = true;
        StateHasChanged();

        try
        {
            var ipAddress = HttpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            var (session, pin) = await UploadSessionService.CreateSessionAsync(
                currentUserId, 
                expiryMinutes, 
                maxFiles, 
                selectedCategoryId, 
                string.IsNullOrWhiteSpace(sessionLabel) ? null : sessionLabel,
                ipAddress
            );

            generatedPin = pin;
            
            // Get local IP address for mobile access
            var baseUri = Navigation.BaseUri;
            try 
            {
                var hostName = System.Net.Dns.GetHostName();
                var ips = await System.Net.Dns.GetHostAddressesAsync(hostName);
                var lanIp = ips.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork 
                    && !System.Net.IPAddress.IsLoopback(ip));
                
                if (lanIp != null)
                {
                    baseUri = $"http://{lanIp}:5197/";
                }
            }
            catch 
            {
                // Fallback to BaseUri if IP detection fails
            }

            uploadUrl = $"{baseUri}m/upload/{session.Token}";
            qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(uploadUrl)}";
            showQrCode = true;
            
            await LoadSessions();
            NotificationService.Notify(NotificationSeverity.Success, "Success", "Upload session created!");
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to create session.");
        }
        finally
        {
            isGenerating = false;
            StateHasChanged();
        }
    }

    private async Task RevokeSession(int sessionId)
    {
        var result = await UploadSessionService.RevokeSessionAsync(sessionId, currentUserId);
        if (result)
        {
            NotificationService.Notify(NotificationSeverity.Info, "Revoked", "Session has been revoked.");
            await LoadSessions();
        }
    }

    private void ResetForm()
    {
        showQrCode = false;
        generatedPin = "";
        uploadUrl = "";
        sessionLabel = "";
    }
    }
}
