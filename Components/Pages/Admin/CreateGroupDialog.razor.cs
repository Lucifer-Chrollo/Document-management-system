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

namespace DocumentManagementSystem.Components.Pages.Admin
{
    public partial class CreateGroupDialog
    {
private UserGroup group = new();
    private bool canRead = true;
    private bool canWrite = false;
    private bool canDelete = false;

    private void OnSubmit(UserGroup model)
    {
        model.DefaultRights = (canRead ? 1 : 0) | (canWrite ? 2 : 0) | (canDelete ? 4 : 0);
        DialogService.Close(model);
    }
    }
}
