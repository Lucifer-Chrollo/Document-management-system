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

namespace DocumentManagementSystem.Components.Pages.Admin
{
    public partial class GroupUsersDialog
    {
[Parameter] public int GroupId { get; set; }
    [Parameter] public int currentUserId { get; set; }

    private string newGroupName = string.Empty;
    private IEnumerable<UserGroup> groups = Enumerable.Empty<UserGroup>();
    private UserGroup? selectedGroup;
    private IEnumerable<UserGroupMember> members = Enumerable.Empty<UserGroupMember>();
    private List<UserGroupMember> pendingMembers = new(); // Local list for when GroupId == 0
    private IEnumerable<ApplicationUser> availableUsers = Enumerable.Empty<ApplicationUser>();
    
    private int selectedUserIdToAdd;
    private bool isLoadingMembers = true;

    // UI Bindings for permissions (integers mapped to booleans)
    private bool canReadBool;
    private bool canWriteBool;
    private bool canDeleteBool;

    protected override async Task OnInitializedAsync()
    {
        await LoadBaseData();
        if (GroupId > 0)
        {
            await LoadGroupDetails();
        }
        else
        {
            isLoadingMembers = false;
        }
    }

    private async Task LoadBaseData()
    {
        groups = await UserGroupService.GetAllGroupsAsync();
        availableUsers = await UserGroupService.SearchUsersAsync(""); // load all users initially
    }

    private async Task LoadGroupDetails()
    {
        isLoadingMembers = true;
        
        selectedGroup = groups.FirstOrDefault(g => g.GroupId == GroupId);
        if (selectedGroup != null)
        {
            canReadBool = (selectedGroup.DefaultRights & 1) == 1;
            canWriteBool = (selectedGroup.DefaultRights & 2) == 2;
            canDeleteBool = (selectedGroup.DefaultRights & 4) == 4;
        }

        members = await UserGroupService.GetGroupMembersDetailedAsync(GroupId);
        selectedUserIdToAdd = 0; // reset dropdown
        
        isLoadingMembers = false;
        StateHasChanged();
    }

    private async Task OnGroupChanged(object value)
    {
        if (value is int newGroupId)
        {
            GroupId = newGroupId;
            await LoadGroupDetails();
        }
    }

    private async Task OnPermissionChanged(int bit, bool isChecked)
    {
        if (GroupId == 0)
        {
            // Being constructed, do not auto-save.
            return;
        }

        if (selectedGroup == null) return;

        // Toggle the specific bit in DefaultRights
        if (isChecked)
            selectedGroup.DefaultRights |= bit;
        else
            selectedGroup.DefaultRights &= ~bit;

        // Auto-save changes
        var response = await UserGroupService.UpdateGroupAsync(selectedGroup, currentUserId);
        
        if (response.Result)
        {
            var permName = bit switch { 1 => "Read", 2 => "Write", 4 => "Delete", _ => "" };
            // Unobtrusive save
        }
        else
        {
            // Revert on failure
            if (isChecked)
                selectedGroup.DefaultRights &= ~bit;
            else
                selectedGroup.DefaultRights |= bit;

            canReadBool = (selectedGroup.DefaultRights & 1) == 1;
            canWriteBool = (selectedGroup.DefaultRights & 2) == 2;
            canDeleteBool = (selectedGroup.DefaultRights & 4) == 4;
            NotificationService.Notify(NotificationSeverity.Error, "Error", response.Message);
        }
    }

    private async Task CreateNewGroup()
    {
        if (string.IsNullOrWhiteSpace(newGroupName)) return;

        int defaultRights = 0;
        if (canReadBool) defaultRights |= 1;
        if (canWriteBool) defaultRights |= 2;
        if (canDeleteBool) defaultRights |= 4;

        var newGroup = new UserGroup 
        { 
            GroupName = newGroupName.Trim(),
            DefaultRights = defaultRights
        };

        var response = await UserGroupService.CreateGroupAsync(newGroup, currentUserId);
        if (response.Result)
        {
            int newId = (int)response.RecordID;
            
            // Add any pending members to the database
            foreach (var pm in pendingMembers)
            {
                await UserGroupService.AddUserToGroupAsync(pm.UserId, newId);
            }
            
            NotificationService.Notify(NotificationSeverity.Success, "Success", "Group created and users added successfully.");
            
            // Re-load groups to get the new group in the dropdown list
            await LoadBaseData();
            
            // Switch out of "Create" mode into "Edit" mode for the newly created group
            GroupId = newId;
            pendingMembers.Clear();
            await LoadGroupDetails();
        }
        else
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", response.Message);
        }
    }

    private async Task OnAddUser()
    {
        if (selectedUserIdToAdd <= 0) return;

        if (GroupId == 0)
        {
            // NEW GROUP CREATION MODE (Pending)
            if (pendingMembers.Any(m => m.UserId == selectedUserIdToAdd))
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Info", "User is already in this group.");
                return;
            }
            
            var user = availableUsers.FirstOrDefault(u => u.Id == selectedUserIdToAdd);
            if (user != null)
            {
                var displayName = string.IsNullOrWhiteSpace(user.FirstName + user.LastName) 
                    ? user.UserName 
                    : $"{user.FirstName} {user.LastName}".Trim();

                pendingMembers.Add(new UserGroupMember 
                { 
                    UserId = user.Id, 
                    UserName = displayName,
                    DepartmentName = user.Department ?? ""
                });
                pendingMembers = pendingMembers.ToList(); // Force reference change so RadzenDataGrid rerenders
                selectedUserIdToAdd = 0; // reset dropdown
                StateHasChanged();
            }
        }
        else
        {
            // EXISTING GROUP (Direct DB Insert)
            if (selectedGroup == null) return;
            
            if (members.Any(m => m.UserId == selectedUserIdToAdd))
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Info", "User is already in this group.");
                return;
            }

            var response = await UserGroupService.AddUserToGroupAsync(selectedUserIdToAdd, GroupId);
            
            if (response.Result)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Added", "User successfully added to group.");
                await LoadGroupDetails(); // refresh member list
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", response.Message);
            }
        }
    }

    private async Task OnRemoveUser(UserGroupMember member)
    {
        if (GroupId == 0)
        {
            // Remove from local list
            pendingMembers.Remove(member);
            pendingMembers = pendingMembers.ToList(); // Force reference change
            StateHasChanged();
        }
        else
        {
            // Real DB delete
            var response = await UserGroupService.RemoveUserFromGroupAsync(member.UserId, GroupId);
            
            if (response.Result)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Removed", "User removed from group.");
                await LoadGroupDetails(); // refresh member list
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", response.Message);
            }
        }
    }
    }
}
