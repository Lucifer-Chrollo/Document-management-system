using DocumentManagementSystem.Models;

namespace DocumentManagementSystem.Store;

// Actions for loading documents
public record LoadDocumentsAction(int? CategoryId, int ParentId);
public record DocumentsLoadedAction(IEnumerable<Document> Documents);
public record LoadDocumentsFailedAction(string ErrorMessage);

// Actions for mutations (Syncing)
public record DocumentChangedAction(string Action, int DocumentId);
public record ClearDocumentsAction();
