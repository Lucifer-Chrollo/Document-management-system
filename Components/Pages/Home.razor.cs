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
using Microsoft.AspNetCore.SignalR.Client;


namespace DocumentManagementSystem.Components.Pages
{
    public partial class Home
    {
private int totalDocuments;
    private long totalSize;
    private IEnumerable<Document> recentDocuments = Enumerable.Empty<Document>();
    private HubConnection? hubConnection;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();

        hubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/documentHub"))
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<string, int>("DocumentChanged", async (action, docId) =>
        {
            await InvokeAsync(async () =>
            {
                await LoadDataAsync();
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
    }

    private async Task LoadDataAsync()
    {
        totalDocuments = await DocumentService.GetCountAsync();
        totalSize = await DocumentService.GetTotalSizeAsync();
        var allDocs = await DocumentService.GetAllAsync();
        recentDocuments = allDocs.Take(10).ToList();
    }

    private void OnDocumentClick(DataGridRowMouseEventArgs<Document> args)
    {
        NavigationManager.NavigateTo($"/documents/{args.Data.DocumentId}");
    }

    private async Task DownloadDocument(Document doc)
    {
        // TODO: Implement download
        await Task.CompletedTask;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

     private static string GetFileIcon(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".pdf" => "picture_as_pdf", ".doc" or ".docx" => "description",
        ".xls" or ".xlsx" => "table_chart", ".jpg" or ".jpeg" or ".png" => "image",
        _ => "insert_drive_file"
    };

    private static string GetIconClass(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".pdf" => "pdf", ".doc" or ".docx" => "word", ".xls" or ".xlsx" => "excel",
        ".jpg" or ".jpeg" or ".png" => "image", _ => "default"
    };

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }
    }
}
