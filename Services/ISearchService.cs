using DocumentManagementSystem.Models;

namespace DocumentManagementSystem.Services;

public interface ISearchService
{
    /// <summary>
    /// Adds or updates a document in the search index.
    /// </summary>
    void IndexDocument(Document document);
    Task IndexDocumentAsync(Document document);

    /// <summary>
    /// Deletes a document from the index.
    /// </summary>
    void DeleteDocument(int documentId);
    Task DeleteDocumentAsync(int documentId);

    /// <summary>
    /// Searches for documents matching the query.
    /// Returns a list of Document IDs.
    /// </summary>
    IEnumerable<int> Search(string query);
    Task<IEnumerable<int>> SearchAsync(string query);

    /// <summary>
    /// Clears and rebuilds the entire index.
    /// </summary>
    void RebuildIndex(IEnumerable<Document> documents);
}
