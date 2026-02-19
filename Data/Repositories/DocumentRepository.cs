using Microsoft.Extensions.Logging;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Polly;
using DocumentManagementSystem.Helpers;
using DocumentManagementSystem.Models;
using System.Data;
using System.Data.Common;

namespace DocumentManagementSystem.Data.Repositories;

/// <summary>
/// Professional-grade Data Access Layer for Document Management.
/// Implements the <see cref="IDocumentRepository"/> using the Microsoft Enterprise Library Data Access Block.
/// </summary>
/// <remarks>
/// <b>Logic Overview:</b>
/// - Uses <b>Command Objects</b> (DbCommand) for type-safe parameter handling.
/// - Implements <b>Manual Mapping</b> from IDataReader to Domain Models for maximum performance.
/// - Enforces <b>Row-Level Security</b> directly in SQL for data isolation.
/// </remarks>
public class DocumentRepository : IDocumentRepository
{
    private readonly Database _db;
    private readonly ILogger<DocumentRepository> _logger;

    /// <summary>
    /// Initializes a new instance with the configured Enterprise Library Database.
    /// </summary>
    /// <param name="db">The database provider (e.g., SqlDatabase) registered in Program.cs.</param>
    /// <param name="logger">The logger instance.</param>
    public DocumentRepository(Database db, ILogger<DocumentRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a document by its unique ID.
    /// </summary>
    /// <remarks>
    /// <b>SQL Strategy:</b> Performs a <b>LEFT JOIN</b> with Categories, Departments, Locations, and Users 
    /// to fetch all required metadata in a single round-trip to the server.
    /// </remarks>
    public async Task<Document?> GetByIdAsync(int id)
    {
        var sql = @"
            SELECT d.DocumentId, d.DocumentName, d.FileType, d.CategoryID, d.UploadedBy, d.Path, d.SourcePath, d.Extension, d.Password, d.Status, d.ParentID, d.UploadedDate, d.UpdatedDate, d.DepartmentID, d.LocationID, d.UpdatedBy, d.FileSize, d.FileHash, d.CompressionAlgorithm, d.CompressedSize, d.BatchLabel, d.CurrentVersion,
                   c.CategoryName, dep.DepartmentName, loc.LocationName, u1.UserName as UserName, u2.UserName as UpdatedByName
            FROM Documents d
            LEFT JOIN Categories c ON d.CategoryID = c.CategoryId
            LEFT JOIN Departments dep ON d.DepartmentID = dep.DepartmentId
            LEFT JOIN Locations loc ON d.LocationID = loc.LocationId
            LEFT JOIN Users u1 ON d.UploadedBy = u1.Id
            LEFT JOIN Users u2 ON d.UpdatedBy = u2.Id
            WHERE d.DocumentId = @Id";

        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@Id", DbType.Int32, id);

        // ExecuteReader is used for efficient, forward-only data streaming.
        using var reader = _db.ExecuteReader(command);
        if (reader.Read())
        {
            return MapDocument(reader);
        }
        return null;
    }

    /// <summary>
    /// Retrieves a list of documents based on dynamic filtering and security constraints.
    /// </summary>
    /// <remarks>
    /// <b>Pro-Level Logic:</b>
    /// - <b>Dynamic SQL:</b> Builds the filter clause based on provided parameters (Category, Parent, Date).
    /// - <b>Security Filter:</b> If <paramref name="userId"/> is provided, it injects a security sub-query 
    ///   to ensure the user only sees files they own or have shared access to via groups.
    /// </remarks>
    public async Task<IEnumerable<Document>> GetAllAsync(int? categoryId = null, int parentId = 0, bool includeDeleted = false, string? batchLabel = null, int? year = null, int? month = null, int? day = null, int? userId = null)
    {
        var sql = @"
            SELECT d.DocumentId, d.DocumentName, d.FileType, d.CategoryID, d.UploadedBy, d.Path, d.SourcePath, d.Extension, d.Password, d.Status, d.ParentID, d.UploadedDate, d.UpdatedDate, d.DepartmentID, d.LocationID, d.UpdatedBy, d.FileSize, d.FileHash, d.CompressionAlgorithm, d.CompressedSize, d.BatchLabel, d.CurrentVersion,
                   c.CategoryName, dep.DepartmentName, loc.LocationName, u1.UserName as UserName, u2.UserName as UpdatedByName
            FROM Documents d
            LEFT JOIN Categories c ON d.CategoryID = c.CategoryId
            LEFT JOIN Departments dep ON d.DepartmentID = dep.DepartmentId
            LEFT JOIN Locations loc ON d.LocationID = loc.LocationId
            LEFT JOIN Users u1 ON d.UploadedBy = u1.Id
            LEFT JOIN Users u2 ON d.UpdatedBy = u2.Id
            WHERE d.ParentID = @ParentId";

        // Logic: Dynamically append filters to minimize data transfer.
        // if (!includeDeleted) sql += " AND d.IsDeleted = 0"; // Removed for schema compatibility
        
        if (categoryId.HasValue)
            sql += " AND d.CategoryID = @CategoryId";

        if (!string.IsNullOrEmpty(batchLabel))
        {
            if (batchLabel == "[No Label]")
                sql += " AND d.BatchLabel IS NULL";
            else
                sql += " AND d.BatchLabel = @BatchLabel";
        }

        if (year.HasValue)
            sql += " AND YEAR(d.UploadedDate) = @Year";
        if (month.HasValue)
            sql += " AND MONTH(d.UploadedDate) = @Month";
        if (day.HasValue)
            sql += " AND DAY(d.UploadedDate) = @Day";

        if (userId.HasValue)
        {
            // Logic: Row-Level Security check. 
            // Ensures the database engine handles the permission check (faster than filtering in C#).
            sql += @" AND (d.UploadedBy = @UserId 
                           OR d.DocumentId IN (SELECT DocumentId FROM UserDocumentRights 
                                               WHERE UserId = @UserId 
                                               OR GroupId IN (SELECT GroupId FROM UserGroupMembers WHERE UserId = @UserId)))";
        }

        sql += " ORDER BY d.UploadedDate DESC";

        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@ParentId", DbType.Int32, parentId);
        if (categoryId.HasValue) _db.AddInParameter(command, "@CategoryId", DbType.Int32, categoryId.Value);
        if (!string.IsNullOrEmpty(batchLabel) && batchLabel != "[No Label]") _db.AddInParameter(command, "@BatchLabel", DbType.String, batchLabel);
        if (year.HasValue) _db.AddInParameter(command, "@Year", DbType.Int32, year.Value);
        if (month.HasValue) _db.AddInParameter(command, "@Month", DbType.Int32, month.Value);
        if (day.HasValue) _db.AddInParameter(command, "@Day", DbType.Int32, day.Value);
        if (userId.HasValue) _db.AddInParameter(command, "@UserId", DbType.Int32, userId.Value);

        var list = new List<Document>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(MapDocument(reader));
        }
        return list;
    }

    /// <summary>
    /// Persists a new document record.
    /// </summary>
    /// <returns>The newly generated Document ID.</returns>
    public async Task<int> CreateAsync(Document document)
    {
        var sql = @"
            INSERT INTO Documents (DocumentName, FileType, CategoryID, UploadedBy, Path, SourcePath, Extension, Password, Status, ParentID, UploadedDate, UpdatedDate, DepartmentID, LocationID, UpdatedBy, FileSize, FileHash, IsDeleted, CompressionAlgorithm, CompressedSize, BatchLabel)
            VALUES (@DocumentName, @FileType, @CategoryID, @UploadedBy, @Path, @SourcePath, @Extension, @Password, @Status, @ParentID, @UploadedDate, @UpdatedDate, @DepartmentID, @LocationID, @UpdatedBy, @FileSize, @FileHash, @IsDeleted, @CompressionAlgorithm, @CompressedSize, @BatchLabel);
            SELECT CAST(SCOPE_IDENTITY() as int);";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        AddDocumentParameters(command, document);
        
        // ExecuteScalar is used to retrieve the single IDENTITY value.
        var id = Convert.ToInt32(_db.ExecuteScalar(command));
        document.DocumentId = id;
        return id;
    }

    /// <summary>
    /// Updates an existing document record.
    /// </summary>
    public async Task UpdateAsync(Document document)
    {
        var sql = @"
            UPDATE Documents SET 
                DocumentName = @DocumentName, FileType = @FileType, CategoryID = @CategoryID, Path = @Path, 
                SourcePath = @SourcePath, Extension = @Extension, Password = @Password, Status = @Status, 
                ParentID = @ParentID, UpdatedDate = @UpdatedDate, 
                DepartmentID = @DepartmentID, LocationID = @LocationID, UpdatedBy = @UpdatedBy, 
                FileSize = @FileSize, FileHash = @FileHash, IsDeleted = @IsDeleted,
                CompressionAlgorithm = @CompressionAlgorithm, CompressedSize = @CompressedSize, 
                BatchLabel = @BatchLabel, CurrentVersion = @CurrentVersion
            WHERE DocumentId = @DocumentId";

        DbCommand command = _db.GetSqlStringCommand(sql);
        AddDocumentParameters(command, document);
        _db.AddInParameter(command, "@DocumentId", DbType.Int32, document.DocumentId);
        _db.AddInParameter(command, "@CurrentVersion", DbType.Int32, document.CurrentVersion);

        _db.ExecuteNonQuery(command);
    }

    /// <summary>
    /// Retrieves documents within a specific parent folder.
    /// </summary>
    public async Task<IEnumerable<Document>> GetByParentAsync(int parentId)
    {
        var sql = @"
            SELECT d.*, c.CategoryName, dep.DepartmentName, loc.LocationName, u1.UserName as UserName, u2.UserName as UpdatedByName
            FROM Documents d
            LEFT JOIN Categories c ON d.CategoryID = c.CategoryId
            LEFT JOIN Departments dep ON d.DepartmentID = dep.DepartmentId
            LEFT JOIN Locations loc ON d.LocationID = loc.LocationId
            LEFT JOIN Users u1 ON d.UploadedBy = u1.Id
            LEFT JOIN Users u2 ON d.UpdatedBy = u2.Id
            WHERE d.IsDeleted = 0 AND d.ParentID = @Id
            ORDER BY d.UploadedDate DESC";

        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@Id", DbType.Int32, parentId);

        var list = new List<Document>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(MapDocument(reader));
        }
        return list;
    }

    /// <summary>
    /// Performs a soft-delete on a document.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        DbCommand command = _db.GetSqlStringCommand("UPDATE Documents SET IsDeleted = 1 WHERE DocumentId = @Id");
        _db.AddInParameter(command, "@Id", DbType.Int32, id);
        _db.ExecuteNonQuery(command);
    }

    /// <summary>
    /// Gets the total count of non-deleted documents.
    /// </summary>
    public async Task<int> GetCountAsync(int? userId = null)
    {
        var sql = "SELECT COUNT(*) FROM Documents d WHERE d.IsDeleted = 0";
        if (userId.HasValue)
        {
            sql += @" AND (d.UploadedBy = @UserId 
                           OR d.DocumentId IN (SELECT DocumentId FROM UserDocumentRights 
                                               WHERE UserId = @UserId 
                                               OR GroupId IN (SELECT GroupId FROM UserGroupMembers WHERE UserId = @UserId)))";
        }
        DbCommand command = _db.GetSqlStringCommand(sql);
        if (userId.HasValue) _db.AddInParameter(command, "@UserId", DbType.Int32, userId.Value);
        return Convert.ToInt32(_db.ExecuteScalar(command));
    }

    /// <summary>
    /// Finds documents matching a specific file fingerprint (Hash).
    /// Used for duplicate detection logic in <see cref="DocumentService"/>.
    /// </summary>
    public async Task<IEnumerable<Document>> FindByHashAsync(string fileHash)
    {
        var sql = @"
            SELECT d.*, c.CategoryName, dep.DepartmentName, loc.LocationName, u1.UserName as UserName, u2.UserName as UpdatedByName
            FROM Documents d
            LEFT JOIN Categories c ON d.CategoryID = c.CategoryId
            LEFT JOIN Departments dep ON d.DepartmentID = dep.DepartmentId
            LEFT JOIN Locations loc ON d.LocationID = loc.LocationId
            LEFT JOIN Users u1 ON d.UploadedBy = u1.Id
            LEFT JOIN Users u2 ON d.UpdatedBy = u2.Id
            WHERE d.IsDeleted = 0 AND d.FileHash = @Hash
            ORDER BY d.UploadedDate";

        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@Hash", DbType.String, fileHash);

        var list = new List<Document>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(MapDocument(reader));
        }
        return list;
    }

    /// <summary>
    /// Bulk-retrieves documents by ID with integrated permission checks.
    /// </summary>
    /// <param name="ids">Collection of Document IDs.</param>
    /// <param name="userId">If provided, results are limited to those accessible by this user.</param>
    public async Task<IEnumerable<Document>> GetByIdsAsync(IEnumerable<int> ids, int? userId = null)
    {
        if (ids == null || !ids.Any()) return Enumerable.Empty<Document>();

        // Logic: Using IN clause with sanitization via string join (Ids are integers).
        var sql = @"
            SELECT d.*, c.CategoryName, dep.DepartmentName, loc.LocationName, u1.UserName as UserName, u2.UserName as UpdatedByName
            FROM Documents d
            LEFT JOIN Categories c ON d.CategoryID = c.CategoryId
            LEFT JOIN Departments dep ON d.DepartmentID = dep.DepartmentId
            LEFT JOIN Locations loc ON d.LocationID = loc.LocationId
            LEFT JOIN Users u1 ON d.UploadedBy = u1.Id
            LEFT JOIN Users u2 ON d.UpdatedBy = u2.Id
            WHERE d.DocumentId IN (" + string.Join(",", ids) + ")";

        if (userId.HasValue)
        {
            sql += @" AND (d.UploadedBy = @UserId 
                           OR d.DocumentId IN (SELECT DocumentId FROM UserDocumentRights 
                                               WHERE UserId = @UserId 
                                               OR GroupId IN (SELECT GroupId FROM UserGroupMembers WHERE UserId = @UserId)))";
        }

        DbCommand command = _db.GetSqlStringCommand(sql);
        if (userId.HasValue) _db.AddInParameter(command, "@UserId", DbType.Int32, userId.Value);

        var list = new List<Document>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(MapDocument(reader));
        }
        return list;
    }

    /// <summary>
    /// Specialized logic for creating a "Virtual" document (Folder).
    /// </summary>
    public async Task<int> CreateFolderAsync(Document folder)
    {
        var sql = @"
            INSERT INTO Documents (DocumentName, CategoryID, LocationID, Status, FileType, Extension, Path, UploadedDate, UploadedBy, UpdatedBy, IsDeleted, ParentID, FileSize) 
            VALUES (@DocumentName, @CategoryID, @LocationID, 'Folder', 'Folder', 'Folder', 'FOLDER', @UploadedDate, @UploadedBy, @UpdatedBy, 0, @ParentID, 0);
            SELECT CAST(SCOPE_IDENTITY() as int)";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@DocumentName", DbType.String, folder.DocumentName);
        _db.AddInParameter(command, "@CategoryID", DbType.Int32, folder.CategoryID);
        _db.AddInParameter(command, "@LocationID", DbType.Int32, folder.LocationID);
        _db.AddInParameter(command, "@UploadedDate", DbType.DateTime, folder.UploadedDate);
        _db.AddInParameter(command, "@UploadedBy", DbType.Int32, folder.UploadedBy);
        _db.AddInParameter(command, "@UpdatedBy", DbType.Int32, folder.UpdatedBy);
        _db.AddInParameter(command, "@ParentID", DbType.Int32, folder.ParentID);

        return Convert.ToInt32(_db.ExecuteScalar(command));
    }

    /// <summary>
    /// Records a new document version in the history audit.
    /// </summary>
    public async Task CreateVersionAsync(DocumentVersion version)
    {
        var sql = @"
            INSERT INTO DocumentVersions (DocumentId, VersionNumber, FilePath, FileName, FileSize, CreatedBy, CreatedDate)
            VALUES (@DocumentId, @VersionNumber, @FilePath, @FileName, @FileSize, @CreatedBy, @CreatedDate)";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@DocumentId", DbType.Int32, version.DocumentId);
        _db.AddInParameter(command, "@VersionNumber", DbType.Int32, version.VersionNumber);
        _db.AddInParameter(command, "@FilePath", DbType.String, version.FilePath);
        _db.AddInParameter(command, "@FileName", DbType.String, version.FileName);
        _db.AddInParameter(command, "@FileSize", DbType.Int64, version.FileSize);
        _db.AddInParameter(command, "@CreatedBy", DbType.Int32, version.CreatedBy);
        _db.AddInParameter(command, "@CreatedDate", DbType.DateTime, version.CreatedDate);

        _db.ExecuteNonQuery(command);
    }

    /// <summary>
    /// Retrieves all historical versions for a document.
    /// </summary>
    public async Task<IEnumerable<DocumentVersion>> GetHistoryAsync(int documentId)
    {
        var sql = "SELECT * FROM DocumentVersions WHERE DocumentId = @DocId ORDER BY VersionNumber DESC";
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@DocId", DbType.Int32, documentId);

        var list = new List<DocumentVersion>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(MapDocumentVersion(reader));
        }
        return list;
    }

    /// <summary>
    /// Retrieves a specific file version by ID.
    /// </summary>
    public async Task<DocumentVersion?> GetVersionAsync(int versionId)
    {
        var sql = "SELECT * FROM DocumentVersions WHERE VersionId = @Id";
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@Id", DbType.Int32, versionId);

        using var reader = _db.ExecuteReader(command);
        if (reader.Read())
        {
            return MapDocumentVersion(reader);
        }
        return null;
    }

    /// <summary>
    /// Aggregates distinct dates for the "Archive View" UI component.
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetArchiveDatesAsync()
    {
        var sql = @"
            SELECT DISTINCT 
                YEAR(UploadedDate) as Year, 
                MONTH(UploadedDate) as Month, 
                DAY(UploadedDate) as Day
            FROM Documents 
            WHERE IsDeleted = 0
            ORDER BY Year DESC, Month DESC, Day DESC";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        var list = new List<dynamic>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(new { 
                Year = reader.GetInt32(reader.GetOrdinal("Year")),
                Month = reader.GetInt32(reader.GetOrdinal("Month")),
                Day = reader.GetInt32(reader.GetOrdinal("Day"))
            });
        }
        return list;
    }

    /// <summary>
    /// Grants explicit document read/write rights to a user.
    /// </summary>
    public async Task GrantUserAccessAsync(int documentId, int userId, int rights)
    {
        var sql = @"
            IF EXISTS (SELECT 1 FROM UserDocumentRights WHERE DocumentId = @DocId AND UserId = @UserId AND GroupId IS NULL)
                UPDATE UserDocumentRights SET Rights = @Rights WHERE DocumentId = @DocId AND UserId = @UserId AND GroupId IS NULL
            ELSE
                INSERT INTO UserDocumentRights (DocumentId, UserId, Rights) VALUES (@DocId, @UserId, @Rights)";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@DocId", DbType.Int32, documentId);
        _db.AddInParameter(command, "@UserId", DbType.Int32, userId);
        _db.AddInParameter(command, "@Rights", DbType.Int32, rights);
        _db.ExecuteNonQuery(command);
    }

    /// <summary>
    /// Grants explicit document read/write rights to a group.
    /// </summary>
    public async Task GrantGroupAccessAsync(int documentId, int groupId, int rights)
    {
        var sql = @"
            IF EXISTS (SELECT 1 FROM UserDocumentRights WHERE DocumentId = @DocId AND GroupId = @GroupId)
                UPDATE UserDocumentRights SET Rights = @Rights WHERE DocumentId = @DocId AND GroupId = @GroupId
            ELSE
                INSERT INTO UserDocumentRights (DocumentId, UserId, GroupId, Rights) VALUES (@DocId, 0, @GroupId, @Rights)";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@DocId", DbType.Int32, documentId);
        _db.AddInParameter(command, "@GroupId", DbType.Int32, groupId);
        _db.AddInParameter(command, "@Rights", DbType.Int32, rights);
        _db.ExecuteNonQuery(command);
    }

    /// <summary>
    /// Removes individual user permissions for a document.
    /// </summary>
    public async Task RevokeUserAccessAsync(int documentId, int userId)
    {
        DbCommand command = _db.GetSqlStringCommand("DELETE FROM UserDocumentRights WHERE DocumentId = @DocId AND UserId = @UserId AND GroupId IS NULL");
        _db.AddInParameter(command, "@DocId", DbType.Int32, documentId);
        _db.AddInParameter(command, "@UserId", DbType.Int32, userId);
        _db.ExecuteNonQuery(command);
    }

    /// <summary>
    /// Removes group-wide permissions for a document.
    /// </summary>
    public async Task RevokeGroupAccessAsync(int documentId, int groupId)
    {
        DbCommand command = _db.GetSqlStringCommand("DELETE FROM UserDocumentRights WHERE DocumentId = @DocId AND GroupId = @GroupId");
        _db.AddInParameter(command, "@DocId", DbType.Int32, documentId);
        _db.AddInParameter(command, "@GroupId", DbType.Int32, groupId);
        _db.ExecuteNonQuery(command);
    }

    /// <summary>
    /// Reverses a soft-delete operation.
    /// </summary>
    public async Task<bool> RestoreAsync(int id)
    {
        var sql = "UPDATE Documents SET IsDeleted = 0, Status = 'Active', UpdatedDate = @Date WHERE DocumentId = @Id";
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@Id", DbType.Int32, id);
        _db.AddInParameter(command, "@Date", DbType.DateTime, DateTime.UtcNow);
        var rows = _db.ExecuteNonQuery(command);
        return rows > 0;
    }

    /// <summary>
    /// Calculates the sum of all stored file sizes.
    /// </summary>
    public async Task<long> GetTotalSizeAsync(int? userId = null)
    {
        var sql = "SELECT SUM(d.FileSize) FROM Documents d WHERE d.IsDeleted = 0";
        if (userId.HasValue)
        {
            sql += @" AND (d.UploadedBy = @UserId 
                           OR d.DocumentId IN (SELECT DocumentId FROM UserDocumentRights 
                                               WHERE UserId = @UserId 
                                               OR GroupId IN (SELECT GroupId FROM UserGroupMembers WHERE UserId = @UserId)))";
        }
        DbCommand command = _db.GetSqlStringCommand(sql);
        if (userId.HasValue) _db.AddInParameter(command, "@UserId", DbType.Int32, userId.Value);
        var result = _db.ExecuteScalar(command);
        return result != DBNull.Value ? Convert.ToInt64(result) : 0L;
    }

    /// <summary>
    /// Identifies all document records sharing the same file content (Hash).
    /// Used for administrative cleanup and storage optimization.
    /// </summary>
    public async Task<IEnumerable<IGrouping<string, Document>>> GetDuplicatesAsync()
    {
        var sql = @"
            SELECT d.*, c.CategoryName, dep.DepartmentName, loc.LocationName, u1.UserName as UserName, u2.UserName as UpdatedByName
            FROM Documents d
            LEFT JOIN Categories c ON d.CategoryID = c.CategoryId
            LEFT JOIN Departments dep ON d.DepartmentID = dep.DepartmentId
            LEFT JOIN Locations loc ON d.LocationID = loc.LocationId
            LEFT JOIN Users u1 ON d.UploadedBy = u1.Id
            LEFT JOIN Users u2 ON d.UpdatedBy = u2.Id
            WHERE d.IsDeleted = 0 
            AND d.FileHash IS NOT NULL 
            AND d.FileHash IN (
                SELECT FileHash FROM Documents 
                WHERE IsDeleted = 0 AND FileHash IS NOT NULL
                GROUP BY FileHash HAVING COUNT(*) > 1
            )
            ORDER BY d.FileHash, d.UploadedDate";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        var list = new List<Document>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(MapDocument(reader));
        }
        return list.GroupBy(d => d.FileHash!);
    }

    /// <summary>
    /// Calculates storage efficiency statistics (how much space is wasted by duplicates).
    /// </summary>
    public async Task<(int DuplicateGroups, int TotalDuplicateFiles, long WastedBytes)> GetDuplicateStatsAsync()
    {
        var sql = @"
            WITH DuplicateHashes AS (
                SELECT FileHash, COUNT(*) as FileCount, MIN(FileSize) as OriginalSize
                FROM Documents 
                WHERE IsDeleted = 0 AND FileHash IS NOT NULL
                GROUP BY FileHash 
                HAVING COUNT(*) > 1
            )
            SELECT 
                COUNT(*) as DuplicateGroups,
                (SELECT COUNT(*) FROM Documents d WHERE d.IsDeleted = 0 AND d.FileHash IN (SELECT FileHash FROM DuplicateHashes)) as TotalFiles,
                (SELECT SUM((FileCount - 1) * OriginalSize) FROM DuplicateHashes) as WastedBytes";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        using var reader = _db.ExecuteReader(command);
        if (reader.Read())
        {
            return (
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? 0L : reader.GetInt64(2)
            );
        }
        return (0, 0, 0L);
    }

    /// <summary>
    /// Logic helper: Type-safe mapping of domain properties to SQL parameters.
    /// </summary>
    private void AddDocumentParameters(DbCommand command, Document document)
    {
        _db.AddInParameter(command, "@DocumentName", DbType.String, document.DocumentName);
        _db.AddInParameter(command, "@FileType", DbType.String, document.FileType);
        _db.AddInParameter(command, "@CategoryID", DbType.Int32, document.CategoryID);
        _db.AddInParameter(command, "@UploadedBy", DbType.Int32, document.UploadedBy);
        _db.AddInParameter(command, "@Path", DbType.String, document.Path);
        _db.AddInParameter(command, "@SourcePath", DbType.String, document.SourcePath);
        _db.AddInParameter(command, "@Extension", DbType.String, document.Extension);
        _db.AddInParameter(command, "@Password", DbType.String, document.Password);
        _db.AddInParameter(command, "@Status", DbType.String, document.Status);
        _db.AddInParameter(command, "@ParentID", DbType.Int32, document.ParentID);
        _db.AddInParameter(command, "@UploadedDate", DbType.DateTime, document.UploadedDate);
        _db.AddInParameter(command, "@UpdatedDate", DbType.DateTime, document.UpdatedDate);
        _db.AddInParameter(command, "@DepartmentID", DbType.Int32, document.DepartmentID);
        _db.AddInParameter(command, "@LocationID", DbType.Int32, document.LocationID);
        _db.AddInParameter(command, "@UpdatedBy", DbType.Int32, document.UpdatedBy);
        _db.AddInParameter(command, "@FileSize", DbType.Int64, document.FileSize);
        _db.AddInParameter(command, "@FileHash", DbType.String, document.FileHash);
        _db.AddInParameter(command, "@IsDeleted", DbType.Boolean, document.IsDeleted);
        _db.AddInParameter(command, "@CompressionAlgorithm", DbType.String, document.CompressionAlgorithm);
        _db.AddInParameter(command, "@CompressedSize", DbType.Int64, document.CompressedSize);
        _db.AddInParameter(command, "@BatchLabel", DbType.String, document.BatchLabel);
    }

    /// <summary>
    /// Logic helper: Transforms a flat database row into a rich <see cref="Document"/> object.
    /// Handles null-safety and type conversions.
    /// </summary>
    private Document MapDocument(IDataReader reader)
    {
        var doc = new Document
        {
            DocumentId = GetInt(reader, "DocumentId"),
            DocumentName = GetString(reader, "DocumentName"),
            FileType = GetString(reader, "FileType"),
            CategoryID = GetInt(reader, "CategoryID"),
            UploadedBy = GetInt(reader, "UploadedBy"),
            Path = GetString(reader, "Path"),
            SourcePath = GetString(reader, "SourcePath"),
            Extension = GetString(reader, "Extension"),
            Password = GetString(reader, "Password"),
            Status = GetString(reader, "Status"),
            ParentID = GetInt(reader, "ParentID"),
            UploadedDate = GetDateTime(reader, "UploadedDate"),
            UpdatedDate = GetDateTime(reader, "UpdatedDate"),
            DepartmentID = GetInt(reader, "DepartmentID"),
            LocationID = GetInt(reader, "LocationID"),
            UpdatedBy = GetInt(reader, "UpdatedBy"),
            FileSize = GetLong(reader, "FileSize"),
            FileHash = GetString(reader, "FileHash"),
            // IsDeleted = GetBool(reader, "IsDeleted"), // Removed
            // IsOcrProcessed = GetBool(reader, "IsOcrProcessed"), // Removed for simplicity/perf
            // OcrText = GetString(reader, "OcrText"), // Removed BIG FIELD
            // OcrConfidence = GetDecimal(reader, "OcrConfidence"),
            // OcrEngine = GetString(reader, "OcrEngine"),
            // OcrProcessedDate = GetNullableDateTime(reader, "OcrProcessedDate"),
            // HasExtractedText = GetBool(reader, "HasExtractedText"),
            CompressionAlgorithm = GetString(reader, "CompressionAlgorithm"),
            CompressedSize = GetNullableLong(reader, "CompressedSize"),
            BatchLabel = GetString(reader, "BatchLabel"),
            CurrentVersion = GetInt(reader, "CurrentVersion"),
            
            // Joined Fields (metadata from related tables)
            CategoryName = GetString(reader, "CategoryName"),
            DepartmentName = GetString(reader, "DepartmentName"),
            LocationName = GetString(reader, "LocationName"),
            UserName = GetString(reader, "UserName"),
            UpdatedByName = GetString(reader, "UpdatedByName")
        };
        return doc;
    }

    /// <summary>
    /// Logic helper: Transforms a flat database row into a <see cref="DocumentVersion"/> object.
    /// </summary>
    private DocumentVersion MapDocumentVersion(IDataReader reader)
    {
        return new DocumentVersion
        {
            VersionId = GetInt(reader, "VersionId"),
            DocumentId = GetInt(reader, "DocumentId"),
            VersionNumber = GetInt(reader, "VersionNumber"),
            FilePath = GetString(reader, "FilePath"),
            FileName = GetString(reader, "FileName"),
            FileSize = GetLong(reader, "FileSize"),
            CreatedDate = GetDateTime(reader, "CreatedDate"),
            CreatedBy = GetInt(reader, "CreatedBy")
        };
    }

    // Performance & Safety Helpers: Centralized logic for reading reader values safely.
    private string GetString(IDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? string.Empty : reader.GetString(reader.GetOrdinal(column));
    private int GetInt(IDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? 0 : reader.GetInt32(reader.GetOrdinal(column));
    private long GetLong(IDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? 0L : reader.GetInt64(reader.GetOrdinal(column));
    private decimal GetDecimal(IDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? 0m : reader.GetDecimal(reader.GetOrdinal(column));
    private bool GetBool(IDataReader reader, string column) => !reader.IsDBNull(reader.GetOrdinal(column)) && reader.GetBoolean(reader.GetOrdinal(column));
    private DateTime GetDateTime(IDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal(column));
    private DateTime? GetNullableDateTime(IDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetDateTime(reader.GetOrdinal(column));
    private long? GetNullableLong(IDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetInt64(reader.GetOrdinal(column));

    public async Task<int?> GetUserIdByNameAsync(string username)
    {
        var sql = "SELECT TOP 1 Id FROM Users WHERE UserName = @Name OR FirstName = @Name";
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@Name", DbType.String, username);
        var result = _db.ExecuteScalar(command);
        return result != null && result != DBNull.Value ? Convert.ToInt32(result) : null;
    }

    public async Task<int?> GetGroupIdByNameAsync(string groupName)
    {
        var sql = "SELECT TOP 1 GroupId FROM UserGroups WHERE GroupName = @Name";
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@Name", DbType.String, groupName);
        var result = _db.ExecuteScalar(command);
        return result != null && result != DBNull.Value ? Convert.ToInt32(result) : null;
    }

    public async Task<IEnumerable<UserDocumentRights>> GetRightsAsync(int documentId)
    {
        var sql = @"
            SELECT r.RightId, r.UserId, r.DocumentId, r.Rights, r.GroupId,
                   COALESCE(u.UserName, g.GroupName) as UserName,
                   CASE r.Rights 
                        WHEN 1 THEN 'Read' 
                        WHEN 2 THEN 'Read/Write' 
                        WHEN 3 THEN 'Full Access' 
                        ELSE 'Unknown' 
                   END as RightsName
            FROM UserDocumentRights r
            LEFT JOIN Users u ON r.UserId = u.Id
            LEFT JOIN UserGroups g ON r.GroupId = g.GroupId
            WHERE r.DocumentId = @DocId";

        DbCommand command = _db.GetSqlStringCommand(sql);
        try
        {
            return ResiliencyPolicies.GetSqlRetryPolicy(_logger, "GetRights").Execute(() =>
            {
                var sql = @"
                    SELECT r.RightId, r.UserId, r.DocumentId, r.Rights, r.GroupId,
                           COALESCE(u.UserName, g.GroupName) as UserName,
                           CASE r.Rights 
                                WHEN 1 THEN 'Read' 
                                WHEN 2 THEN 'Read/Write' 
                                WHEN 3 THEN 'Full Access' 
                                ELSE 'Unknown' 
                           END as RightsName
                    FROM UserDocumentRights r
                    LEFT JOIN Users u ON r.UserId = u.Id
                    LEFT JOIN UserGroups g ON r.GroupId = g.GroupId
                    WHERE r.DocumentId = @DocId";

                DbCommand command = _db.GetSqlStringCommand(sql);
                _db.AddInParameter(command, "@DocId", DbType.Int32, documentId);

                var list = new List<UserDocumentRights>();
                using var reader = _db.ExecuteReader(command);
                while (reader.Read())
                {
                    list.Add(new UserDocumentRights
                    {
                        RightId = GetInt(reader, "RightId"),
                        UserId = GetInt(reader, "UserId"),
                        DocumentId = GetInt(reader, "DocumentId"),
                        Rights = GetInt(reader, "Rights"),
                        GroupId = reader.IsDBNull(reader.GetOrdinal("GroupId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("GroupId")),
                        UserName = GetString(reader, "UserName"),
                        RightsName = GetString(reader, "RightsName")
                    });
                }
                return (IEnumerable<UserDocumentRights>)list;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rights for document {DocumentId}", documentId);
            return Enumerable.Empty<UserDocumentRights>();
        }
    }

    public async Task<IEnumerable<string>> GetBatchLabelsAsync(int? categoryId = null)
    {
        try
        {
            return ResiliencyPolicies.GetSqlRetryPolicy(_logger, "GetBatchLabels").Execute(() =>
            {
                var labels = new List<string>();
                string sql = "SELECT DISTINCT BatchLabel FROM Documents WHERE IsDeleted = 0 AND BatchLabel IS NOT NULL AND BatchLabel <> ''";
                
                if (categoryId.HasValue)
                {
                    sql += " AND CategoryID = @CategoryId";
                }
                
                sql += " ORDER BY BatchLabel";

                DbCommand command = _db.GetSqlStringCommand(sql);
                if (categoryId.HasValue)
                {
                    _db.AddInParameter(command, "@CategoryId", DbType.Int32, categoryId.Value);
                }

                using (IDataReader reader = _db.ExecuteReader(command))
                {
                    while (reader.Read())
                    {
                        labels.Add(Convert.ToString(reader["BatchLabel"]));
                    }
                }
                return (IEnumerable<string>)labels;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch labels");
            return Enumerable.Empty<string>();
        }
    }
}
