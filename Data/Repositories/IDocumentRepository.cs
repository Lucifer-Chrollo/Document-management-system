using DocumentManagementSystem.Models;

namespace DocumentManagementSystem.Data.Repositories;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(int id);
    Task<IEnumerable<Document>> GetAllAsync(int? categoryId = null, int parentId = 0, bool includeDeleted = false, string? batchLabel = null, int? year = null, int? month = null, int? day = null, int? userId = null);
    Task<int> CreateAsync(Document document);
    Task UpdateAsync(Document document);
    Task DeleteAsync(int id);
    Task<IEnumerable<Document>> GetByParentAsync(int parentId);
    Task<int> GetCountAsync(int? userId = null);
    Task<IEnumerable<Document>> FindByHashAsync(string fileHash);
    Task<IEnumerable<Document>> GetByIdsAsync(IEnumerable<int> ids, int? userId = null);
    Task<int> CreateFolderAsync(Document folder);
    Task CreateVersionAsync(DocumentVersion version);
    Task<IEnumerable<DocumentVersion>> GetHistoryAsync(int documentId);
    Task<DocumentVersion?> GetVersionAsync(int versionId);
    Task<IEnumerable<dynamic>> GetArchiveDatesAsync();
    Task GrantUserAccessAsync(int documentId, int userId, int rights);
    Task GrantGroupAccessAsync(int documentId, int groupId, int rights);
    Task RevokeUserAccessAsync(int documentId, int userId);
    Task RevokeGroupAccessAsync(int documentId, int groupId);
    Task<bool> RestoreAsync(int id);
    Task<long> GetTotalSizeAsync(int? userId = null);
    Task<IEnumerable<IGrouping<string, Document>>> GetDuplicatesAsync();
    Task<(int DuplicateGroups, int TotalDuplicateFiles, long WastedBytes)> GetDuplicateStatsAsync();
    Task<int?> GetUserIdByNameAsync(string username);
    Task<int?> GetGroupIdByNameAsync(string groupName);
    Task<IEnumerable<UserDocumentRights>> GetRightsAsync(int documentId);
    Task<IEnumerable<string>> GetBatchLabelsAsync(int? categoryId = null);
}
