using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Radzen;

namespace DocumentManagementSystem.Components.Layout;

/// <summary>
/// Primary application layout.
/// Manages the sidebar, header, global search, error boundary, and real-time notifications.
/// </summary>
public partial class MainLayout : IAsyncDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private NotificationService NotificationService { get; set; } = default!;

    private HubConnection? hubConnection;
    private ErrorBoundary? errorBoundary;
    private bool sidebarExpanded = true;
    private string searchQuery = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/documentHub"))
            .WithAutomaticReconnect()
            .Build();

        // Listen for file-shared notifications
        hubConnection.On<string, string, int>("FileShared", (sharedByUser, documentName, documentId) =>
        {
            InvokeAsync(() =>
            {
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Info,
                    Summary = "File Shared With You",
                    Detail = $"'{documentName}' has been shared with you.",
                    Duration = 6000
                });
                StateHasChanged();
            });
        });

        try
        {
            await hubConnection.StartAsync();
        }
        catch (Exception)
        {
            // Hub connection failure is non-fatal; app continues without real-time notifications
        }
    }

    private void HandleSearch(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            DoSearch();
        }
    }

    private void DoSearch()
    {
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            NavigationManager.NavigateTo($"/search?q={Uri.EscapeDataString(searchQuery)}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }
}
