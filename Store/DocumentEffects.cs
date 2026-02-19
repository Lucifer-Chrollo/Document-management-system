using DocumentManagementSystem.Services;
using Fluxor;

namespace DocumentManagementSystem.Store;

public class DocumentEffects
{
    private readonly IDocumentService _documentService;

    public DocumentEffects(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [EffectMethod]
    public async Task HandleLoadDocumentsAction(LoadDocumentsAction action, IDispatcher dispatcher)
    {
        try
        {
            var documents = await _documentService.GetAllAsync(action.CategoryId, action.ParentId);
            dispatcher.Dispatch(new DocumentsLoadedAction(documents));
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(new LoadDocumentsFailedAction(ex.Message));
        }
    }

    [EffectMethod]
    public async Task HandleDocumentChangedAction(DocumentChangedAction action, IDispatcher dispatcher)
    {
        // When a document changes (broadcast from SignalR), we just trigger a reload of the current view
        // In a more granular "Pro" system, we might update just the single item, 
        // but for now, re-fetching ensures we have the latest server state (metadata, etc).
        // dispatcher.Dispatch(new LoadDocumentsAction(null, 0)); 
        // Note: The UI component will handle dispatching the reload based on its current view.
        await Task.CompletedTask;
    }
}
