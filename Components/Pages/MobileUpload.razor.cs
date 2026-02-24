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
    private List<IBrowserFile> selectedFiles = new();

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
                await LoadSession(); // Refresh session data
            }
            else
            {
                await LoadSession(); // Refresh to get updated failed attempts
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

    private void HandleFileSelected(InputFileChangeEventArgs e)
    {
        try
        {
            var incomingFiles = e.GetMultipleFiles(10000).ToList();
            
            // Prevent adding duplicates by name
            foreach (var file in incomingFiles)
            {
                if (!selectedFiles.Any(f => f.Name == file.Name))
                {
                    selectedFiles.Add(file);
                }
            }
            uploadError = "";
        }
        catch (Exception ex)
        {
            uploadError = $"Selection Error: {ex.Message} (Try selecting fewer files)";
        }
        finally
        {
            StateHasChanged();
        }
    }

    private async Task UploadFiles()
    {
        if (!selectedFiles.Any() || session == null) return;

        uploadError = "";
        isUploading = true;
        uploadedCount = 0;
        StateHasChanged();

        try
        {
            foreach (var file in selectedFiles)
            {
                try
                {
                    using var stream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024); // 100MB max
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    ms.Position = 0;

                    var document = new Document
                    {
                        DocumentName = file.Name,
                        FileType = file.ContentType,
                        CategoryID = session.DefaultCategoryId ?? 1,
                        UploadedBy = session.UserId,
                        Extension = Path.GetExtension(file.Name),
                        FileSize = file.Size,
                        Status = "Active"
                    };

                    var response = await DocumentService.CreateAsync(document, ms);
                    if (!response.Result)
                    {
                        uploadError += $"Error uploading {file.Name}: {response.Message}\n";
                        continue; // Skip incrementing file count for failed document
                    }

                    await UploadSessionService.IncrementFileCountAsync(Token);
                    uploadedCount++;
                }
                catch (Exception ex)
                {
                    // Accumulate errors instead of an outright crash for all files
                    uploadError += $"Error uploading {file.Name}: {ex.Message}\n";
                }
            }

            if (uploadedCount > 0)
            {
                if (string.IsNullOrEmpty(uploadError)) {
                    uploadSuccess = true;
                    selectedFiles.Clear();
                } else {
                    // Remove successfully uploaded files from the list so they aren't retried
                    selectedFiles.RemoveAll(f => !uploadError.Contains(f.Name));
                }
                
                await LoadSession(); // Refresh counts
            }
        }
        finally
        {
            isUploading = false;
            StateHasChanged();
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
    }
}
