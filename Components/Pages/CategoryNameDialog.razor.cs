using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;

namespace DocumentManagementSystem.Components.Pages
{
    public partial class CategoryNameDialog
    {
[Parameter] public string InitialName { get; set; } = "";
    [Inject] public DialogService DialogService { get; set; } = default!;

    private string categoryName = "";

    protected override void OnInitialized()
    {
        categoryName = InitialName;
    }

    private void OnSave()
    {
        DialogService.Close(categoryName?.Trim());
    }
    }
}
