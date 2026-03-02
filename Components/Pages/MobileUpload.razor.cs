using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;


namespace DocumentManagementSystem.Components.Pages
{
    public partial class MobileUpload
    {
    [Parameter]
    public string Token { get; set; } = "";

    private UploadSession? session;
    private bool isLoading = true;
    private bool isPinVerified;
    private bool isVerifying;
    private bool isUploading;
    private bool uploadSuccess;
    private string enteredPin = "";
    private string pinError = "";
    private string uploadError = "";
    private int uploadedCount;
    private int fileCount;

    protected override async Task OnInitializedAsync()
    {
        await LoadSession();
    }

    private async Task LoadSession()
    {
        isLoading = true;
        try
        {
            session = await UploadSessionService.GetByTokenAsync(Token);
            if (session != null)
            {
                isPinVerified = session.IsPinVerified;
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task VerifyPin()
    {
        pinError = "";
        isVerifying = true;
        StateHasChanged();

        try
        {
            if (string.IsNullOrEmpty(enteredPin) || enteredPin.Length != 6)
            {
                pinError = "Please enter a 6-digit PIN.";
                return;
            }

            var ipAddress = HttpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            var result = await UploadSessionService.VerifyPinAsync(Token, enteredPin, ipAddress);

            if (result)
            {
                isPinVerified = true;
                await LoadSession();
            }
            else
            {
                await LoadSession();
                if (session != null && session.FailedAttempts >= session.MaxAttempts)
                {
                    pinError = "Session locked due to too many failed attempts.";
                }
                else
                {
                    pinError = $"Invalid PIN. {session?.MaxAttempts - session?.FailedAttempts} attempts remaining.";
                }
                enteredPin = "";
            }
        }
        finally
        {
            isVerifying = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Called from JavaScript when files are selected via the native file picker.
    /// </summary>
    [JSInvokable]
    public static Task UpdateFileCount(int count)
    {
        _currentInstance?.SetFileCount(count);
        return Task.CompletedTask;
    }

    [JSInvokable]
    public static Task SetUploadingState(bool uploading)
    {
        _currentInstance?.SetUploading(uploading);
        return Task.CompletedTask;
    }

    [JSInvokable]
    public static async Task OnUploadComplete(int successCount, string errors)
    {
        if (_currentInstance != null)
        {
            await _currentInstance.HandleUploadComplete(successCount, errors);
        }
    }

    private static MobileUpload? _currentInstance;

    protected override void OnInitialized()
    {
        _currentInstance = this;
    }

    private void SetFileCount(int count)
    {
        fileCount = count;
        InvokeAsync(StateHasChanged);
    }

    private void SetUploading(bool uploading)
    {
        isUploading = uploading;
        InvokeAsync(StateHasChanged);
    }

    private async Task HandleUploadComplete(int successCount, string errors)
    {
        uploadedCount = successCount;
        isUploading = false;
        fileCount = 0;

        if (successCount > 0 && string.IsNullOrEmpty(errors))
        {
            uploadSuccess = true;
        }
        else if (!string.IsNullOrEmpty(errors))
        {
            uploadError = errors;
        }

        await LoadSession();
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        if (_currentInstance == this)
        {
            _currentInstance = null;
        }
    }
    }
}
