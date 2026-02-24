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

namespace DocumentManagementSystem.Components.Pages
{
    public partial class MfaSetup
    {
private bool isLoading = true;
    private bool isMfaEnabled;
    private DateTime? mfaSetupDate;
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

    private async Task LoadMfaStatus()
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
                    mfaSetupDate = currentUser.MfaSetupDate;
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
                errorMessage = "Please enter a valid 6-digit code.";
                return;
            }

            if (!MfaService.ValidateCode(tempSecret, verificationCode))
            {
                errorMessage = "Invalid code. Please try again.";
                return;
            }

            // Save MFA secret to user
            if (currentUser != null)
            {
                currentUser.MfaSecret = tempSecret;
                currentUser.TwoFactorEnabled = true;
                currentUser.MfaSetupDate = DateTime.UtcNow;
                
                var result = await UserManager.UpdateAsync(currentUser);
                if (result.Succeeded)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success", 
                        "Two-factor authentication has been enabled!");
                    isMfaEnabled = true;
                    mfaSetupDate = currentUser.MfaSetupDate;
                    setupStep = 0;
                }
                else
                {
                    errorMessage = "Failed to save MFA settings.";
                }
            }
        }
        finally
        {
            isVerifying = false;
            StateHasChanged();
        }
    }

    private async Task DisableMfa()
    {
        if (currentUser != null)
        {
            currentUser.MfaSecret = null;
            currentUser.TwoFactorEnabled = false;
            currentUser.MfaSetupDate = null;
            
            var result = await UserManager.UpdateAsync(currentUser);
            if (result.Succeeded)
            {
                NotificationService.Notify(NotificationSeverity.Info, "Disabled", 
                    "Two-factor authentication has been disabled.");
                isMfaEnabled = false;
            }
        }
    }
    }
}
