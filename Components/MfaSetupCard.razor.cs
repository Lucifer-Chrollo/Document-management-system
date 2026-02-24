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

namespace DocumentManagementSystem.Components
{
    public partial class MfaSetupCard
    {
[Parameter] public EventCallback OnChanged { get; set; }

    private bool isLoading = true;
    private bool isMfaEnabled;
    private int setupStep = 0;
    private string tempSecret = "";
    private string provisioningUri = "";
    private string verificationCode = "";
    private string errorMessage = "";
    private bool isVerifying;
    private ApplicationUser? currentUser;

    protected override async Task OnInitializedAsync()
    {
        await LoadMfaStatus();
    }

    public async Task LoadMfaStatus()
    {
        isLoading = true;
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated == true)
            {
                currentUser = await UserManager.GetUserAsync(authState.User);
                if (currentUser != null)
                {
                    isMfaEnabled = currentUser.TwoFactorEnabled;
                }
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private void StartSetup()
    {
        tempSecret = MfaService.GenerateSecret();
        provisioningUri = MfaService.GetProvisioningUri(tempSecret, currentUser?.Email ?? "user", "DMS");
        setupStep = 1;
    }

    private async Task VerifyAndEnable()
    {
        errorMessage = "";
        isVerifying = true;
        StateHasChanged();

        try
        {
            if (string.IsNullOrEmpty(verificationCode) || verificationCode.Length != 6)
            {
                errorMessage = "Invalid code.";
                return;
            }

            if (!MfaService.ValidateCode(tempSecret, verificationCode))
            {
                errorMessage = "Wrong code.";
                return;
            }

            if (currentUser != null)
            {
                currentUser.MfaSecret = tempSecret;
                currentUser.TwoFactorEnabled = true;
                currentUser.MfaSetupDate = DateTime.UtcNow;
                
                var result = await UserManager.UpdateAsync(currentUser);
                if (result.Succeeded)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success", "2FA Enabled!");
                    isMfaEnabled = true;
                    setupStep = 0;
                    await OnChanged.InvokeAsync();
                }
                else
                {
                    errorMessage = "Save failed.";
                }
            }
        }
        finally
        {
            isVerifying = false;
            StateHasChanged();
        }
    }
    
    private void OnManageClick()
    {
        // Navigate or simple disable? 
        // User said "exact working", so maybe just disable button?
        // But "UI Card" implies compact.
        // I'll reset to step 0 allow re-setup or disable?
        // For now, I'll toggle a small menu or just disable.
        // Actually, I'll show a "Disable" button directly here.
        setupStep = 99; // Manage Mode
    }
    
    private async Task DisableMfa()
    {
        if (currentUser != null)
        {
            currentUser.MfaSecret = null;
            currentUser.TwoFactorEnabled = false;
            currentUser.MfaSetupDate = null;
            await UserManager.UpdateAsync(currentUser);
            isMfaEnabled = false;
            setupStep = 0;
            NotificationService.Notify(NotificationSeverity.Info, "Disabled", "2FA Disabled.");
            await OnChanged.InvokeAsync();
        }
    }

    // Add Manage Mode UI rendering in main block (simple switch)
    }
}
