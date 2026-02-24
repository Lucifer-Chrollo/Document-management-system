using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using Radzen;

namespace DocumentManagementSystem.Components.Pages
{
    public partial class CreateCategoryDialog
    {
[Parameter] public string categoryName { get; set; } = string.Empty;

    private void OnSave()
    {
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            DialogService.Close(categoryName);
        }
    }
    }
}
