using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using Radzen;
using Radzen.Blazor;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace DocumentManagementSystem.Components.Pages.Admin
{
    public partial class UserGroups
    {
private IEnumerable<UserGroup> groups = Enumerable.Empty<UserGroup>();
    private bool isLoading = true;
    private int currentUserId;
    private bool isAdmin = false;
    private string newGroupName = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            isAdmin = user.Identity.Name?.ToLower() == "admin";
            var idClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            int.TryParse(idClaim?.Value, out currentUserId);
        }
        await LoadGroups();
    }

    private async Task LoadGroups()
    {
        isLoading = true;
        try 
        { 
            if (isAdmin)
                groups = await UserGroupService.GetAllGroupsAsync();
            else
                groups = await UserGroupService.GetGroupsCreatedByUserAsync(currentUserId); 
        }
        finally { isLoading = false; }
    }

    private async Task OnAddGroup()
    {
        var result = await DialogService.OpenAsync<GroupUsersDialog>("Group Users", 
            new Dictionary<string, object> { { "GroupId", 0 }, { "currentUserId", currentUserId } },
            new DialogOptions() { Width = "950px", Height = "500px", Resizable = true, Draggable = true });
        
        // Reload groups after the dialog closes (if a new group was created/saved)
        await LoadGroups();
    }

    private async Task OnEditGroup(UserGroup group)
    {
        var result = await DialogService.OpenAsync<GroupUsersDialog>("Group Users", 
            new Dictionary<string, object> { { "GroupId", group.GroupId }, { "currentUserId", currentUserId } },
            new DialogOptions() { Width = "950px", Height = "500px", Resizable = true, Draggable = true });
        
        // Reload in case permissions or counts changed
        await LoadGroups();
    }

    private async Task OnDeleteGroup(UserGroup group)
    {
        var confirmed = await DialogService.Confirm($"Delete group '{group.GroupName}'?", "Confirm Delete");
        if (confirmed == true)
        {
            var response = await UserGroupService.DeleteGroupAsync(group.GroupId, currentUserId);
            if (response.Result)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", "Group removed successfully.");
                await LoadGroups();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", response.Message);
            }
        }
    }
    }
}
