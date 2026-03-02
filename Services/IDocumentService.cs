using DocumentManagementSystem.Models;

namespace DocumentManagementSystem.Services;

public interface IDocumentService
{
    // CRUD
    Document? GetById(int id);
    Task<Document?> GetByIdAsync(int id);
    IEnumerable<Document> GetAll(int? categoryId = null, int parentId = 0, string? batchLabel = null, bool includeDeleted = false, int? year = null, int? month = null, int? day = null);
    Task<IEnumerable<Document>> GetAllAsync(int? categoryId = null, int parentId = 0, string? batchLabel = null, bool includeDeleted = false, int? year = null, int? month = null, int? day = null);
    Task<iFishResponse> CreateAsync(Document document, Stream fileStream, string? userId = null);
    iFishResponse UploadDocument(DocumentUploadModel model, int userId);
    Task<iFishResponse> UploadDocumentAsync(DocumentUploadModel model, int userId);
    Document GetDocument(int documentId, int userId);
    Task<Document> GetDocumentAsync(int documentId, int userId);
    iFishResponse DeleteDocument(int documentId, int userId);
    Task<iFishResponse> DeleteDocumentAsync(int documentId, int userId);
    IEnumerable<Document> SearchDocuments(string query, int userId);
    Task<IEnumerable<Document>> SearchDocumentsAsync(string query, int userId);
    Stream DownloadDocument(int documentId, int userId);
    Task<Stream> DownloadDocumentAsync(int documentId, int userId);
    Task<iFishResponse> UpdateAsync(Document document, string? userId = null);
    Task<iFishResponse> DeleteAsync(int id);
    Task<iFishResponse> RestoreAsync(int id);
    Task<iFishResponse> PermanentlyDeleteAsync(int id);
    Task<IEnumerable<Document>> GetDeletedDocumentsAsync();
    
    // Search
    Task<IEnumerable<Document>> SearchAsync(string query, int? categoryId = null, int parentId = 0, string? batchLabel = null, int? year = null, int? month = null, int? day = null);
    
    // Folders & Groups
    Task<iFishResponse> CreateFolderAsync(string folderName, int categoryId, int locationId, string? userId = null);
    Task<IEnumerable<string>> GetBatchLabelsAsync(int? categoryId = null);
    Task<IEnumerable<ArchiveDateNode>> GetArchiveDatesAsync();
    
    // File Operations
    Task<Stream?> DownloadAsync(int id);
    Task<Stream?> DownloadVersionAsync(int versionId);
    Task<DocumentVersion?> GetVersionAsync(int versionId);
    Task<(Stream? Stream, string FileName, string ContentType)> DownloadVersionWithMetaAsync(int versionId);
    Task<string> GetContentTypeAsync(int id);
    
    // Versioning (if maintained)
    // Versioning
    // Versioning
    iFishResponse UpdateWithFile(Document document, Stream fileStream, string? userId = null);
    Task<iFishResponse> UpdateWithFileAsync(Document document, Stream fileStream, string? userId = null);
    IEnumerable<DocumentVersion> GetHistory(int id);
    Task<IEnumerable<DocumentVersion>> GetHistoryAsync(int id);
    iFishResponse RestoreVersion(int versionId, string? userId = null);
    Task<iFishResponse> RestoreVersionAsync(int versionId, string? userId = null);
    
    // File Operations Ext
    Stream? DownloadVersion(int versionId);
    (Stream? Stream, string FileName, string ContentType) DownloadVersionWithMeta(int versionId);
    
    // Stats
    Task<long> GetTotalSizeAsync();
    Task<int> GetCountAsync();
    
    // Duplicate Detection
    Task<IEnumerable<IGrouping<string, Document>>> GetDuplicatesAsync();
    Task<(int DuplicateGroups, int TotalDuplicateFiles, long WastedBytes)> GetDuplicateStatsAsync();
    Task<IEnumerable<Document>> FindByHashAsync(string fileHash);
    
    // Permissions
    Task<iFishResponse> GrantGroupAccessAsync(int documentId, int groupId, int rights);
    Task<iFishResponse> RevokeGroupAccessAsync(int documentId, int groupId);
    Task<iFishResponse> GrantUserAccessAsync(int documentId, int userId, int rights);
    Task<iFishResponse> RevokeUserAccessAsync(int documentId, int userId);
    Task<IEnumerable<UserDocumentRights>> GetRightsAsync(int documentId);
    
    // Lookups
    Task<IEnumerable<Category>> GetCategoriesAsync();
    Task<IEnumerable<Location>> GetLocationsAsync();
}
