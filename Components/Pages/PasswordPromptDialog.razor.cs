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
    public partial class PasswordPromptDialog
    {
[Parameter]
    public string? ExpectedPassword { get; set; }

    private string password = string.Empty;
    private bool showError = false;
    private string errorMessage = "Incorrect password. Please try again.";

    private void Submit()
    {
        showError = false;
        
        // If an expected password is provided, validation happens right here in the dialog
        if (!string.IsNullOrEmpty(ExpectedPassword))
        {
            if (password == ExpectedPassword)
            {
                DialogService.Close(password);
            }
            else
            {
                showError = true;
            }
        }
        else
        {
            // If no explicit expected password is provided, just return whatever they typed
            // and let the caller validate it (e.g. against an API endpoint)
            DialogService.Close(password);
        }
    }

    private void Cancel()
    {
        DialogService.Close(null);
    }
    }
}
