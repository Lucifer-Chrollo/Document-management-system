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

    /// <summary>
    /// Broadcasts a message that a document was shared.
    /// </summary>
    public async Task NotifyFileShared(string sharedByUser, string documentName, int documentId, int? groupId = null)
    {
        // Broadcast to all clients for now. In a production app, we could map Context.ConnectionId to UserIds
        // and only send to members of that specific group, but this suffices for the notification UI.
        await Clients.Others.SendAsync("FileShared", sharedByUser, documentName, documentId, groupId);
    }
}
