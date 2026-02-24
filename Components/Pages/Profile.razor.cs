using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace DocumentManagementSystem.Components.Pages
{
    public partial class Profile
    {
private ApplicationUser? currentUser;
    private double storagePercentage;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (authState.User.Identity?.IsAuthenticated == true)
        {
            currentUser = await UserManager.GetUserAsync(authState.User);
            if (currentUser != null && currentUser.StorageQuotaBytes > 0)
            {
                storagePercentage = (double)currentUser.StorageUsedBytes / currentUser.StorageQuotaBytes * 100;
            }
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    private string GetInitials()
    {
        var first = !string.IsNullOrEmpty(currentUser?.FirstName) ? currentUser.FirstName[0].ToString().ToUpper() : "";
        var last = !string.IsNullOrEmpty(currentUser?.LastName) ? currentUser.LastName[0].ToString().ToUpper() : "";
        var initials = first + last;
        return string.IsNullOrEmpty(initials) ? (currentUser?.UserName?[0].ToString().ToUpper() ?? "U") : initials;
    }
    }
}
