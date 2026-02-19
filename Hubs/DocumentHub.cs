using Microsoft.AspNetCore.SignalR;

namespace DocumentManagementSystem.Hubs;

/// <summary>
/// Hub for broadcasting document-related changes to all connected clients.
/// </summary>
public class DocumentHub : Hub
{
    /// <summary>
    /// Broadcasts a message that a document or folder has changed.
    /// </summary>
    public async Task NotifyDocumentChanged(string action, int documentId)
    {
        await Clients.Others.SendAsync("DocumentChanged", action, documentId);
    }
}
