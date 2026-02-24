using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Radzen;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;

namespace DocumentManagementSystem.Components.Pages
{
    public partial class Duplicates
    {
private IEnumerable<IGrouping<string, Document>> duplicateGroups = Enumerable.Empty<IGrouping<string, Document>>();
    private (int DuplicateGroups, int TotalDuplicateFiles, long WastedBytes) stats;
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {

        await LoadDuplicates();
    }

    private async Task LoadDuplicates()
    {
        isLoading = true;
        StateHasChanged();

        try
        {
            duplicateGroups = await DocumentService.GetDuplicatesAsync();
            stats = await DocumentService.GetDuplicateStatsAsync();
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load duplicates.");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task DeleteDocument(Document doc)
    {
        var confirmed = await DialogService.Confirm(
            $"Delete '{doc.DocumentName}'? This will move it to trash.",
            "Delete Duplicate",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed == true)
        {
            await DocumentService.DeleteAsync(doc.DocumentId);
            NotificationService.Notify(NotificationSeverity.Success, "Deleted", "Document moved to trash.");
            await LoadDuplicates();
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

    private static string GetFileIcon(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".pdf" => "picture_as_pdf",
        ".doc" or ".docx" => "description",
        ".xls" or ".xlsx" => "table_chart",
        ".jpg" or ".jpeg" or ".png" => "image",
        _ => "insert_drive_file"
    };
    }
}
