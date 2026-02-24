using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;


namespace DocumentManagementSystem.Components.Pages.Admin
{
    public partial class Documents
    {
private IEnumerable<Document> allDocuments = Enumerable.Empty<Document>();
    private IEnumerable<Document> filteredDocuments = Enumerable.Empty<Document>();
    private bool isLoading = true;
    private Dictionary<int, string> revealedPasswords = new();
    private RadzenDataGrid<Document>? grid;
    private string searchQuery = string.Empty;
    private string? selectedFilter;
    private int protectedCount;

    private List<string> filterOptions = new()
    {
        "All Documents",
        "Password Protected Only",
        "No Password",
        "Active Only",
        "Deleted Only"
    };

    protected override async Task OnInitializedAsync()
    {
        // Admin-only guard
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        if (user.Identity?.Name?.ToLower() != "admin" && !user.IsInRole("Admin"))
        {
            NavigationManager.NavigateTo("/");
            return;
        }

        await LoadDocuments();
    }

    private async Task LoadDocuments()
    {
        isLoading = true;
        try
        {
            allDocuments = await DocumentService.GetAllAsync(includeDeleted: true);
            protectedCount = allDocuments.Count(d => !string.IsNullOrEmpty(d.Password));
            ApplyFilter();
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to load documents: {ex.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var docs = allDocuments.AsEnumerable();

        // Text search
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            docs = docs.Where(d =>
                d.DocumentName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                (d.UserName?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.CategoryName?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Filter dropdown
        switch (selectedFilter)
        {
            case "Password Protected Only":
                docs = docs.Where(d => !string.IsNullOrEmpty(d.Password));
                break;
            case "No Password":
                docs = docs.Where(d => string.IsNullOrEmpty(d.Password));
                break;
            case "Active Only":
                docs = docs.Where(d => !d.IsDeleted && d.Status == "Active");
                break;
            case "Deleted Only":
                docs = docs.Where(d => d.IsDeleted);
                break;
        }

        filteredDocuments = docs.ToList();
        StateHasChanged();
    }

    private void ClearFilters()
    {
        searchQuery = string.Empty;
        selectedFilter = null;
        ApplyFilter();
    }

    private void RevealPassword(Document doc)
    {
        if (!string.IsNullOrEmpty(doc.Password))
        {
            revealedPasswords[doc.DocumentId] = doc.Password;
        }
        StateHasChanged();
    }

    private async Task CopyPassword(Document doc)
    {
        if (revealedPasswords.TryGetValue(doc.DocumentId, out var pwd))
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", pwd);
                NotificationService.Notify(NotificationSeverity.Success, "Copied", "Password copied to clipboard.");
            }
            catch
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Copy Failed", "Could not copy to clipboard.");
            }
        }
    }

    private void ViewDocument(Document doc)
    {
        // Navigate to the document view â€” admin auto-bypasses password
        NavigationManager.NavigateTo($"/documents/{doc.DocumentId}");
    }

    private async Task DownloadDocument(Document doc)
    {
        try
        {
            // Admin bypasses password at API level, so no pwd needed
            var url = $"/api/files/download/{doc.DocumentId}";
            var fileName = doc.DocumentName + (doc.Extension ?? "");
            await JSRuntime.InvokeVoidAsync("triggerFileDownload", url, fileName);
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Download failed: {ex.Message}");
        }
    }

    private async Task RemovePassword(Document doc)
    {
        var confirmed = await DialogService.Confirm(
            $"Remove password protection from '{doc.DocumentName}'?\n\nThe user who set this password will no longer need a password to access this document.",
            "Remove Password",
            new ConfirmOptions { OkButtonText = "Remove Password", CancelButtonText = "Cancel" });

        if (confirmed == true)
        {
            try
            {
                doc.Password = null;
                var result = await DocumentService.UpdateAsync(doc);
                if (result.Result)
                {
                    revealedPasswords.Remove(doc.DocumentId);
                    protectedCount = allDocuments.Count(d => !string.IsNullOrEmpty(d.Password));
                    NotificationService.Notify(NotificationSeverity.Success, "Updated", "Password protection removed.");
                    StateHasChanged();
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error", result.Message ?? "Failed to update document.");
                }
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to remove password: {ex.Message}");
            }
        }
    }

    private async Task DeleteDocument(Document doc)
    {
        var confirmed = await DialogService.Confirm($"Delete document '{doc.DocumentName}'?", "Delete Document",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed == true)
        {
            await DocumentService.DeleteAsync(doc.DocumentId);
            await LoadDocuments();
            NotificationService.Notify(NotificationSeverity.Success, "Deleted", "Document moved to trash.");
        }
    }

    private static string GetFileIcon(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".pdf" => "picture_as_pdf",
        ".doc" or ".docx" => "description",
        ".xls" or ".xlsx" => "table_chart",
        ".jpg" or ".jpeg" or ".png" or ".webp" => "image",
        _ => "insert_drive_file"
    };

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
