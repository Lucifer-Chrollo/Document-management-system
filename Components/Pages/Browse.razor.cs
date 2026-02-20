using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using DocumentManagementSystem.Data.Repositories;
using System.Collections.Generic;
using System.Linq;

namespace DocumentManagementSystem.Components.Pages;

public partial class Browse : Fluxor.Blazor.Web.Components.FluxorComponent, IDisposable
{
    [Inject] private IDocumentService DocumentService { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private NotificationService NotificationService { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IDocumentRepository DocumentRepository { get; set; } = default!;

    [Parameter] public int? CategoryId { get; set; }
    [Parameter] public int? ParentId { get; set; }

    private IEnumerable<Document> documents = Enumerable.Empty<Document>();
    private IList<Document> selectedDocuments = new List<Document>();
    private IEnumerable<Category> categories = Enumerable.Empty<Category>();
    private IEnumerable<string> batchLabels = Enumerable.Empty<string>();
    private List<ArchiveDateNode> archiveNodes = new();
    private Document? parentFolder;

    private int? selectedCategoryId;
    private string? selectedBatchLabel;
    private string searchQuery = string.Empty;
    private int? selectedYear;
    private int? selectedMonth;
    private int? selectedDay;

    private bool isLoading = false;
    private bool isDownloading = false;
    private double downloadProgress = 0;
    private long bytesDownloaded = 0;
    private long totalBytesToDownload = 0;
    private DotNetObjectReference<Browse>? dotNetHelper;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        categories = await CategoryService.GetCategoriesAsync();
        batchLabels = await DocumentService.GetBatchLabelsAsync();
        await LoadArchiveNodes();
    }

    protected override async Task OnParametersSetAsync()
    {
        selectedCategoryId = CategoryId;
        await LoadData();
    }

    private async Task LoadData()
    {
        isLoading = true;
        try
        {
            if (ParentId.HasValue && ParentId > 0)
            {
                parentFolder = await DocumentService.GetByIdAsync(ParentId.Value);
                documents = await DocumentService.GetAllAsync(parentId: ParentId.Value);
            }
            else
            {
                parentFolder = null;
                documents = await DocumentService.GetAllAsync(
                    categoryId: selectedCategoryId,
                    batchLabel: selectedBatchLabel,
                    year: selectedYear,
                    month: selectedMonth,
                    day: selectedDay
                );
            }

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                documents = documents.Where(d => 
                    d.DocumentName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (d.BatchLabel?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to load documents: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadArchiveNodes()
    {
        var dates = await DocumentService.GetArchiveDatesAsync();
        var nodes = new List<ArchiveDateNode>();

        foreach (var yearGroup in dates.GroupBy(d => d.Year))
        {
            var yearNode = new ArchiveDateNode { Text = yearGroup.Key.ToString(), Year = yearGroup.Key };
            foreach (var monthGroup in yearGroup.GroupBy(d => d.Month))
            {
                var monthNode = new ArchiveDateNode 
                { 
                    Text = monthGroup.Key.HasValue ? System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(monthGroup.Key.Value) : "Unknown",
                    Year = yearGroup.Key,
                    Month = monthGroup.Key
                };
                foreach (var day in monthGroup)
                {
                    monthNode.Children.Add(new ArchiveDateNode 
                    { 
                        Text = day.Day.ToString(),
                        Year = yearGroup.Key,
                        Month = monthGroup.Key,
                        Day = day.Day
                    });
                }
                yearNode.Children.Add(monthNode);
            }
            nodes.Add(yearNode);
        }
        archiveNodes = nodes;
    }

    private async Task OnCategoryChange(object value)
    {
        if (value is int id)
        {
            NavigationManager.NavigateTo($"/browse/{id}");
        }
        else
        {
            NavigationManager.NavigateTo("/browse");
        }
    }

    private async Task OnTreeChange(TreeEventArgs args)
    {
        if (args.Value is ArchiveDateNode node)
        {
            selectedYear = node.Year;
            selectedMonth = node.Month;
            selectedDay = node.Day;
            await LoadData();
        }
    }

    private async Task ClearDateFilter()
    {
        selectedYear = null;
        selectedMonth = null;
        selectedDay = null;
        await LoadData();
    }

    private async Task Search()
    {
        await LoadData();
    }

    private void GoBack()
    {
        if (parentFolder != null)
        {
            if (parentFolder.ParentID > 0)
                NavigationManager.NavigateTo($"/browse/{CategoryId}/{parentFolder.ParentID}");
            else
                NavigationManager.NavigateTo($"/browse/{CategoryId}");
        }
    }

    private void SelectAll(bool value)
    {
        selectedDocuments = value ? documents.ToList() : new List<Document>();
    }

    private void ToggleSelection(Document doc, bool value)
    {
        if (value)
        {
            if (!selectedDocuments.Contains(doc)) selectedDocuments.Add(doc);
        }
        else
        {
            selectedDocuments.Remove(doc);
        }
    }

    private string GetFileIcon(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".pdf" => "picture_as_pdf",
        ".doc" or ".docx" => "description",
        ".xls" or ".xlsx" => "table_chart",
        ".jpg" or ".jpeg" or ".png" => "image",
        _ => "insert_drive_file"
    };

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
        return $"{size:0.##} {sizes[order]}";
    }

    private async Task PreviewDocument(Document doc)
    {
        await DialogService.OpenAsync<DocumentPreview>(
            $"Preview: {doc.DocumentName}",
            new Dictionary<string, object> { { "DocumentId", doc.DocumentId } },
            new DialogOptions
            {
                Width = "90vw",
                Height = "85vh",
                Resizable = true,
                Draggable = true,
                CloseDialogOnOverlayClick = true
            });
    }

    private async Task DownloadDocument(Document doc)
    {
        isDownloading = true;
        downloadProgress = 0;
        bytesDownloaded = 0;
        totalBytesToDownload = doc.FileSize;
        StateHasChanged();

        try
        {
            dotNetHelper = DotNetObjectReference.Create(this);
            var url = $"/api/files/download/{doc.DocumentId}";
            var fileName = doc.DocumentName + (doc.Extension ?? "");
            await JSRuntime.InvokeVoidAsync("downloadWithProgress", url, fileName, dotNetHelper);
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Download failed: {ex.Message}");
        }
        finally
        {
            isDownloading = false;
            StateHasChanged();
        }
    }

    [JSInvokable]
    public void OnDownloadProgress(long receivedLength, long totalLength)
    {
        bytesDownloaded = receivedLength;
        if (totalLength > 0)
        {
            totalBytesToDownload = totalLength;
            downloadProgress = (double)receivedLength / totalLength * 100;
        }
        else if (totalBytesToDownload > 0)
        {
            downloadProgress = (double)receivedLength / totalBytesToDownload * 100;
        }
        InvokeAsync(StateHasChanged);
    }

    private async Task DeleteDocument(Document doc)
    {
        var confirmed = await DialogService.Confirm($"Delete document '{doc.DocumentName}'?", "Delete");
        if (confirmed == true)
        {
            await DocumentService.DeleteAsync(doc.DocumentId);
            await LoadData();
            NotificationService.Notify(NotificationSeverity.Success, "Success", "Document moved to trash.");
        }
    }

    private async Task DeleteSelected()
    {
        var confirmed = await DialogService.Confirm($"Delete {selectedDocuments.Count} documents?", "Delete Selected");
        if (confirmed == true)
        {
            foreach (var doc in selectedDocuments.ToList())
            {
                await DocumentService.DeleteAsync(doc.DocumentId);
            }
            selectedDocuments.Clear();
            await LoadData();
            NotificationService.Notify(NotificationSeverity.Success, "Success", "Documents deleted.");
        }
    }

    private async Task OnShareDocument(Document doc)
    {
        var result = await DialogService.OpenAsync<ShareDocumentDialog>("Share Document", 
            new Dictionary<string, object> { { "DocumentName", doc.DocumentName } });

        if (result != null)
        {
            try 
            {
                var shareData = (dynamic)result;
                await DocumentRepository.GrantGroupAccessAsync(doc.DocumentId, shareData.GroupId, shareData.Rights);
                NotificationService.Notify(NotificationSeverity.Success, "Shared", $"File successfully shared with the group.");
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Could not share document: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        dotNetHelper?.Dispose();
    }
}
