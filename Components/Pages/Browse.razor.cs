using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Radzen;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using DocumentManagementSystem.Data.Repositories;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR.Client;

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
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [Parameter] public int? CategoryId { get; set; }
    [Parameter] public int? ParentId { get; set; }

    private IEnumerable<Document> documents = Enumerable.Empty<Document>();
    private IList<Document> selectedDocuments = new List<Document>();
    private IEnumerable<Category> categories = Enumerable.Empty<Category>();
    private IEnumerable<string> batchLabels = Enumerable.Empty<string>();
    private int? selectedCategoryId;
    private int? selectedTopCategoryId;
    private string? selectedBatchLabel;
    private string searchQuery = string.Empty;
    private DateTime?[]? dates;
    private Document? parentFolder;

    private bool isLoading = false;
    private bool isDownloading = false;
    private bool initialLoadComplete = false;
    private bool isToolsMenuOpen = false;
    private double downloadProgress = 0;
    private long bytesDownloaded = 0;
    private long totalBytesToDownload = 0;
    private DotNetObjectReference<Browse>? dotNetHelper;
    private HubConnection? hubConnection;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        selectedCategoryId = CategoryId;
        isLoading = true;

        // Resolve auth once
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var isAdmin = user.Identity?.Name?.ToLower() == "admin" || user.IsInRole("Admin");
        var idClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        // Fire all independent data loads in PARALLEL
        var categoryTask = isAdmin
            ? CategoryService.GetCategoriesAsync()
            : (idClaim != null && int.TryParse(idClaim.Value, out var userId))
                ? CategoryService.GetCategoriesByUserAsync(userId)
                : Task.FromResult(Enumerable.Empty<Category>());

        var batchTask = DocumentService.GetBatchLabelsAsync();
        var dataTask = LoadDataCore();

        // Wait for all to finish — runs ~3-4x faster than sequential
        await Task.WhenAll(categoryTask, batchTask, dataTask);

        categories = await categoryTask;
        batchLabels = await batchTask;

        isLoading = false;
        initialLoadComplete = true;
        StateHasChanged();

        // Setup SignalR in background — don't block the page render
        _ = Task.Run(async () =>
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl(NavigationManager.ToAbsoluteUri("/documentHub"))
                .WithAutomaticReconnect()
                .Build();

            hubConnection.On<string, int>("DocumentChanged", async (action, docId) =>
            {
                await InvokeAsync(async () =>
                {
                    await LoadData();
                    StateHasChanged();
                });
            });

            try
            {
                await hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR connection failed: {ex.Message}");
            }
        });
    }

    protected override async Task OnParametersSetAsync()
    {
        // Skip the first call — OnInitializedAsync already loaded data
        if (!initialLoadComplete) return;

        selectedCategoryId = CategoryId;
        await LoadData();
    }

    private async Task LoadData()
    {
        isLoading = true;
        try
        {
            await LoadDataCore();
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

    /// <summary>
    /// Core data loading logic — separated so it can run in Task.WhenAll without StateHasChanged side effects.
    /// </summary>
    private async Task LoadDataCore()
    {
        if (ParentId.HasValue && ParentId > 0)
        {
            parentFolder = await DocumentService.GetByIdAsync(ParentId.Value);
            documents = await DocumentService.GetAllAsync(
                categoryId: selectedCategoryId,
                parentId: ParentId.Value,
                batchLabel: selectedBatchLabel
            );
        }
        else
        {
            parentFolder = null;
            documents = await DocumentService.GetAllAsync(
                categoryId: selectedCategoryId,
                batchLabel: selectedBatchLabel
            );
        }

        // Apply Date Range filter in memory
        if (dates != null && dates.Length >= 1 && dates[0].HasValue)
        {
            var startDate = dates[0].Value.Date;
            if (dates.Length > 1 && dates[1].HasValue)
            {
                var endDate = dates[1].Value.Date.AddDays(1).AddTicks(-1);
                documents = documents.Where(d => d.UploadedDate >= startDate && d.UploadedDate <= endDate).ToList();
            }
            else
            {
                documents = documents.Where(d => d.UploadedDate.Date == startDate).ToList();
            }
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            documents = documents.Where(d =>
                d.DocumentName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                (d.BatchLabel?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }
    }

    private async Task OnTopCategoryChange(object value)
    {
        if (value is int id)
        {
            selectedCategoryId = id;
            await LoadData();
        }
        else
        {
            selectedCategoryId = null;
            await LoadData();
        }
    }

    private async Task OnDateRangeChange(object value)
    {
        if (value is DateTime?[] range)
        {
            dates = range;
            await LoadData();
        }
    }

    private void ToggleToolsMenu()
    {
        isToolsMenuOpen = !isToolsMenuOpen;
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
        if (!string.IsNullOrEmpty(doc.Password))
        {
            // Admin users bypass password prompt
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            var userIsAdmin = user.Identity?.Name?.ToLower() == "admin" || user.IsInRole("Admin");

            if (!userIsAdmin)
            {
                var result = await DialogService.OpenAsync<PasswordPromptDialog>("Password Required", 
                    new Dictionary<string, object> { { "ExpectedPassword", doc.Password } },
                    new DialogOptions() { Width = "400px", Height = "250px", Resizable = false, Draggable = false });
                
                if (result == null) return; // User cancelled
            }
        }

        await DialogService.OpenAsync<DocumentPreview>(
            $"Preview: {doc.DocumentName}",
            new Dictionary<string, object> { 
                { "DocumentId", doc.DocumentId },
                { "Password", doc.Password }
            },
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
        if (!string.IsNullOrEmpty(doc.Password))
        {
            // Admin users bypass password prompt
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            var userIsAdmin = user.Identity?.Name?.ToLower() == "admin" || user.IsInRole("Admin");

            if (!userIsAdmin)
            {
                var result = await DialogService.OpenAsync<PasswordPromptDialog>("Password Required", 
                    new Dictionary<string, object> { { "ExpectedPassword", doc.Password } },
                    new DialogOptions() { Width = "400px", Height = "250px", Resizable = false, Draggable = false });
                
                if (result == null) return; // User cancelled
            }
        }

        isDownloading = true;
        downloadProgress = 0;
        bytesDownloaded = 0;
        totalBytesToDownload = doc.FileSize;
        StateHasChanged();

        try
        {
            dotNetHelper = DotNetObjectReference.Create(this);
            var url = $"/api/files/download/{doc.DocumentId}";
            if (!string.IsNullOrEmpty(doc.Password))
            {
                url += $"?pwd={Uri.EscapeDataString(doc.Password)}";
            }
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
        // Resolve current user info for the share dialog
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var userIsAdmin = user.Identity?.Name?.ToLower() == "admin" || user.IsInRole("Admin");
        int userId = 0;
        var idClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (idClaim != null) int.TryParse(idClaim.Value, out userId);

        var result = await DialogService.OpenAsync<ShareDocumentDialog>("Share Document", 
            new Dictionary<string, object> {
                { "DocumentName", doc.DocumentName },
                { "UserId", userId },
                { "IsAdmin", userIsAdmin }
            });

        if (result != null)
        {
            try 
            {
                var shareData = (dynamic)result;
                await DocumentRepository.GrantGroupAccessAsync(doc.DocumentId, shareData.GroupId, shareData.Rights);
                
                // Notify others that a file was shared
                if (hubConnection is not null)
                {
                    var userName = user.Identity?.Name ?? "A user";
                    int groupId = shareData.GroupId; // Explicit cast to avoid dynamic dispatch error on extension method SendAsync
                    await hubConnection.SendAsync("NotifyFileShared", userName, doc.DocumentName, doc.DocumentId, groupId);
                }

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
        if (hubConnection is not null)
        {
            _ = hubConnection.DisposeAsync();
        }
    }
}
