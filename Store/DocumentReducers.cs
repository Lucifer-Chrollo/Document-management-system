using Fluxor;

namespace DocumentManagementSystem.Store;

public static class DocumentReducers
{
    [ReducerMethod]
    public static DocumentsState ReduceLoadDocumentsAction(DocumentsState state, LoadDocumentsAction action) =>
        new DocumentsState(
            isLoading: true, 
            documents: state.Documents, 
            errorMessage: null, 
            currentParentId: action.ParentId, 
            currentCategoryId: action.CategoryId);

    [ReducerMethod]
    public static DocumentsState ReduceDocumentsLoadedAction(DocumentsState state, DocumentsLoadedAction action) =>
        new DocumentsState(
            isLoading: false, 
            documents: action.Documents, 
            errorMessage: null, 
            currentParentId: state.CurrentParentId, 
            currentCategoryId: state.CurrentCategoryId);

    [ReducerMethod]
    public static DocumentsState ReduceLoadDocumentsFailedAction(DocumentsState state, LoadDocumentsFailedAction action) =>
        new DocumentsState(
            isLoading: false, 
            documents: state.Documents, 
            errorMessage: action.ErrorMessage, 
            currentParentId: state.CurrentParentId, 
            currentCategoryId: state.CurrentCategoryId);

    [ReducerMethod]
    public static DocumentsState ReduceClearDocumentsAction(DocumentsState state, ClearDocumentsAction action) =>
        new DocumentsState(false, Enumerable.Empty<Models.Document>(), null, 0, null);
}
