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
using System.Security.Claims;

namespace DocumentManagementSystem.Components
{
    public partial class MobileUploadCard
    {
private bool isLoading = true;
    private bool isCreating = false;
    private bool showQrCode = false;
    private bool isGenerating = false;
    
    private int expiryMinutes = 15;
    private int activeSessionsCount => activeSessions.Count();
    private string qrCodeUrl = "";
    private string generatedPin = "";
    private int createdExpiry = 15;
    
    private IEnumerable<UploadSession> activeSessions = Enumerable.Empty<UploadSession>();
    private int currentUserId;

    private readonly List<object> expiryOptions = new()
    {
        new { Text = "15 min", Value = 15 },
        new { Text = "1 hour", Value = 60 },
        new { Text = "24 hours", Value = 1440 }
    };

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var uid))
        {
            currentUserId = uid;
        }
        await LoadSessions();
    }

    private async Task LoadSessions()
    {
        isLoading = true;
        try
        {
            var allSessions = await UploadSessionService.GetUserSessionsAsync(currentUserId);
            activeSessions = allSessions.Where(s => s.IsActive && DateTime.UtcNow < s.ExpiresAt).ToList();
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task GenerateSession()
    {
        isGenerating = true;
        StateHasChanged();

        try
        {
            var ipAddress = HttpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            // Default max files to 10000 for quick card
            var (session, pin) = await UploadSessionService.CreateSessionAsync(
                currentUserId, 
                expiryMinutes, 
                10000, 
                null, 
                "Quick Card Session", 
                ipAddress
            );

            generatedPin = pin;
            createdExpiry = expiryMinutes;

            // IP Detection Logic
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
            catch {}

            var uploadUrl = $"{baseUri}m/upload/{session.Token}";
            qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={Uri.EscapeDataString(uploadUrl)}";
            showQrCode = true;
            isCreating = false;
            
            // Background refresh
            _ = LoadSessions();
            NotificationService.Notify(NotificationSeverity.Success, "Success", "Scan QR to upload!");
        }
        catch (Exception)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to generate.");
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
            NotificationService.Notify(NotificationSeverity.Info, "Revoked", "Session ended.");
            await LoadSessions();
        }
    }

    private void ResetView()
    {
        showQrCode = false;
        generatedPin = "";
        isCreating = false;
        StateHasChanged();
        _ = LoadSessions();
    }
    }
}
