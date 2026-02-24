using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Radzen;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;


namespace DocumentManagementSystem.Components.Pages
{
    public partial class UploadVersionDialog
    {
[Parameter] public int DocumentId { get; set; }
    [Parameter] public string DocumentName { get; set; } = string.Empty;

    private IBrowserFile? file;
    private bool isUploading = false;
    private long maxFileSize = 100 * 1024 * 1024; // 100MB

    private void LoadFile(InputFileChangeEventArgs e)
    {
        file = e.File;
    }

    private async Task Upload()
    {
        if (file == null) return;

        isUploading = true;
        try
        {
            // Prepare document object for update
            var docUpdate = new Document 
            { 
                DocumentId = DocumentId,
                DocumentName = file.Name, // Use new filename ? Or keep old? Usually we update name to match new file.
                FileType = file.ContentType,
                FileSize = file.Size,
                Extension = Path.GetExtension(file.Name)
            };

            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            using var stream = file.OpenReadStream(maxFileSize);
            await DocumentService.UpdateWithFileAsync(docUpdate, stream, userId);

            NotificationService.Notify(NotificationSeverity.Success, "Success", "New version uploaded successfully.");
            DialogService.Close(true);
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Upload failed: {ex.Message}");
        }
        finally
        {
            isUploading = false;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
        return $"{size:0.##} {sizes[order]}";
    }
    }
}
