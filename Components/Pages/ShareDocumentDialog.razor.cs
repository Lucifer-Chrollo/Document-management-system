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

namespace DocumentManagementSystem.Components.Pages
{
    public partial class ShareDocumentDialog
    {
[Parameter] public string DocumentName { get; set; } = "";
    [Parameter] public int UserId { get; set; }
    [Parameter] public bool IsAdmin { get; set; }
    
    private IEnumerable<UserGroup> groups = Enumerable.Empty<UserGroup>();
    private int selectedGroupId;
    private int rights = 1;

    protected override async Task OnInitializedAsync()
    {
        if (IsAdmin || UserId == 0)
        {
            // Admin sees all groups
            groups = await UserGroupService.GetAllGroupsAsync();
        }
        else
        {
            // Regular user: groups they created + groups they belong to
            var created = await UserGroupService.GetGroupsCreatedByUserAsync(UserId);
            var memberOf = await UserGroupService.GetGroupsForUserAsync(UserId);

            groups = created
                .Union(memberOf, new UserGroupComparer())
                .OrderBy(g => g.GroupName)
                .ToList();
        }
    }

    private void OnShare()
    {
        DialogService.Close(new { GroupId = selectedGroupId, Rights = rights });
    }

    /// <summary>Deduplicates groups by GroupId when merging created + member-of lists.</summary>
    private class UserGroupComparer : IEqualityComparer<UserGroup>
    {
        public bool Equals(UserGroup? x, UserGroup? y) => x?.GroupId == y?.GroupId;
        public int GetHashCode(UserGroup obj) => obj.GroupId.GetHashCode();
    }
    }
}
