using DocumentManagementSystem.Models;
using Fluxor;

namespace DocumentManagementSystem.Store;

/// <summary>
/// Centralized state for the document library.
/// </summary>
[FeatureState]
public class DocumentsState
{
    public bool IsLoading { get; }
    public IEnumerable<Document> Documents { get; }
    public string? ErrorMessage { get; }
    public int CurrentParentId { get; }
    public int? CurrentCategoryId { get; }

    private DocumentsState()
    {
        IsLoading = false;
        Documents = Enumerable.Empty<Document>();
        ErrorMessage = null;
        CurrentParentId = 0;
        CurrentCategoryId = null;
    }

    public DocumentsState(bool isLoading, IEnumerable<Document> documents, string? errorMessage, int currentParentId, int? currentCategoryId)
    {
        IsLoading = isLoading;
        Documents = documents ?? Enumerable.Empty<Document>();
        ErrorMessage = errorMessage;
        CurrentParentId = currentParentId;
        CurrentCategoryId = currentCategoryId;
    }
}
