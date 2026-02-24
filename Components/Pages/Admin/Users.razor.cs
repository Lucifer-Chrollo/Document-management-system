using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;


namespace DocumentManagementSystem.Components.Pages.Admin
{
    public partial class Users
    {
private IEnumerable<ApplicationUser> users = Enumerable.Empty<ApplicationUser>();
    private bool isLoading = true;
    private Dictionary<int, string> revealedPasswords = new();
    private RadzenDataGrid<ApplicationUser>? grid;

    protected override async Task OnInitializedAsync()
    {
        // Admin-only guard
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        if (user.Identity?.Name?.ToLower() != "admin")
        {
            NavigationManager.NavigateTo("/");
            return;
        }

        await LoadUsers();
    }

    private async Task LoadUsers()
    {
        isLoading = true;
        try
        {
            var rawUsers = await UserGroupService.GetAllUsersAsync();
            // Decrypt emails for display
            foreach (var u in rawUsers)
            {
                if (!string.IsNullOrEmpty(u.Email))
                {
                    try { u.Email = await EncryptionService.DecryptTextAsync(u.Email); }
                    catch { /* keep as is if decryption fails */ }
                }
            }
            users = rawUsers.ToList();
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to load users: {ex.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task RevealPassword(ApplicationUser user)
    {
        try
        {
            if (string.IsNullOrEmpty(user.EncryptedPassword))
            {
                revealedPasswords[user.Id] = "(not stored)";
                return;
            }

            var decrypted = await EncryptionService.DecryptTextAsync(user.EncryptedPassword);
            revealedPasswords[user.Id] = decrypted;
        }
        catch (Exception)
        {
            revealedPasswords[user.Id] = "(decrypt error)";
        }
        StateHasChanged();
    }

    private async Task ToggleUserStatus(ApplicationUser user)
    {
        user.IsActive = !user.IsActive;
        var result = await UserManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            var status = user.IsActive ? "activated" : "deactivated";
            NotificationService.Notify(NotificationSeverity.Success, "Updated", $"User {user.UserName} {status}.");
        }
        else
        {
            user.IsActive = !user.IsActive; // revert
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to update user status.");
        }
    }
    }
}
