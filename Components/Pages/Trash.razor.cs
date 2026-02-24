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
    public partial class Trash
    {
private IEnumerable<Document> documents = Enumerable.Empty<Document>();
    private IList<Document> selectedDocuments = new List<Document>();
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {

        await LoadData();
    }

    private async Task LoadData()
    {
        isLoading = true;
        try
        {
            documents = await DocumentService.GetDeletedDocumentsAsync();
            selectedDocuments.Clear();
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load trash items.");
        }
        finally
        {
            isLoading = false;
        }
    }

    private void SelectAll(bool selected)
    {
        selectedDocuments = selected ? documents.ToList() : new List<Document>();
    }

    private void ToggleSelection(Document doc, bool selected)
    {
        if (selected && !selectedDocuments.Contains(doc))
            selectedDocuments.Add(doc);
        else if (!selected)
            selectedDocuments.Remove(doc);
    }

    private async Task RestoreDocument(Document doc)
    {
        await DocumentService.RestoreAsync(doc.DocumentId);
        NotificationService.Notify(NotificationSeverity.Success, "Restored", $"Document '{doc.DocumentName}' restored.");
        await LoadData();
    }

    private async Task RestoreSelected()
    {
        foreach (var doc in selectedDocuments)
        {
            await DocumentService.RestoreAsync(doc.DocumentId);
        }
        NotificationService.Notify(NotificationSeverity.Success, "Restored", $"{selectedDocuments.Count} documents restored.");
        await LoadData();
    }

    private async Task DeleteDocumentForever(Document doc)
    {
        var confirmed = await DialogService.Confirm("Are you sure? This cannot be undone.", "Delete Forever", new ConfirmOptions() { OkButtonText = "Delete Forever", CancelButtonText = "Cancel" });
        if (confirmed == true)
        {
            await DocumentService.PermanentlyDeleteAsync(doc.DocumentId);
            NotificationService.Notify(NotificationSeverity.Warning, "Deleted", $"Document permanently deleted.");
            await LoadData();
        }
    }

    private async Task DeleteSelectedForever()
    {
        var confirmed = await DialogService.Confirm($"Permanently delete {selectedDocuments.Count} items?", "Delete Forever", new ConfirmOptions() { OkButtonText = "Delete Forever", CancelButtonText = "Cancel" });
        if (confirmed == true)
        {
            foreach (var doc in selectedDocuments)
            {
                await DocumentService.PermanentlyDeleteAsync(doc.DocumentId);
            }
            NotificationService.Notify(NotificationSeverity.Warning, "Deleted", $"{selectedDocuments.Count} documents permanently deleted.");
            await LoadData();
        }
    }

    private static string GetFileIcon(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".pdf" => "picture_as_pdf", ".doc" or ".docx" => "description",
        ".xls" or ".xlsx" => "table_chart", ".jpg" or ".jpeg" or ".png" => "image",
        _ => "insert_drive_file"
    };
    }
}
