using DocumentManagementSystem.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Practices.EnterpriseLibrary.Data;
using System.Data;
using System.Data.Common;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Audit trail service for logging document actions.
/// Uses Enterprise Library Database for consistent data access with DocumentRepository and UserGroupService.
/// </summary>
public class AuditService : IAuditService
{
    private readonly Database _db;
    private readonly ILogger<AuditService> _logger;
    private readonly IUserContextService _userContext;

    #region SQL Queries

    private const string SQL_INSERT_AUDIT_LOG = @"
        INSERT INTO AuditLogs (Action, DocumentId, DocumentName, UserId, UserName, IpAddress, UserAgent, Timestamp, Details, Success, ErrorMessage)
        VALUES (@Action, @DocumentId, @DocumentName, @UserId, @UserName, @IpAddress, @UserAgent, @Timestamp, @Details, @Success, @ErrorMessage)";

    #endregion

    public AuditService(Database db, ILogger<AuditService> logger, IUserContextService userContext)
    {
        _db = db;
        _logger = logger;
        _userContext = userContext;
    }

    public void Log(string action, int? documentId, string? documentName, int userId, string userName,
                               string? ipAddress = null, string? userAgent = null, string? details = null,
                               bool success = true, string? errorMessage = null)
    {
        try
        {
            DbCommand command = _db.GetSqlStringCommand(SQL_INSERT_AUDIT_LOG);
            _db.AddInParameter(command, "@Action", DbType.String, action);
            _db.AddInParameter(command, "@DocumentId", DbType.Int32, (object?)documentId ?? DBNull.Value);
            _db.AddInParameter(command, "@DocumentName", DbType.String, (object?)documentName ?? DBNull.Value);
            _db.AddInParameter(command, "@UserId", DbType.Int32, userId);
            _db.AddInParameter(command, "@UserName", DbType.String, userName);
            _db.AddInParameter(command, "@IpAddress", DbType.String, (object?)ipAddress ?? DBNull.Value);
            _db.AddInParameter(command, "@UserAgent", DbType.String, (object?)userAgent ?? DBNull.Value);
            _db.AddInParameter(command, "@Timestamp", DbType.DateTime, DateTime.UtcNow);
            _db.AddInParameter(command, "@Details", DbType.String, (object?)details ?? DBNull.Value);
            _db.AddInParameter(command, "@Success", DbType.Boolean, success);
            _db.AddInParameter(command, "@ErrorMessage", DbType.String, (object?)errorMessage ?? DBNull.Value);

            _db.ExecuteNonQuery(command);
            _logger.LogDebug("Audit: {Action} on {Document} by {User}", action, documentName ?? documentId?.ToString() ?? "N/A", userName);
        }
        catch (Exception ex)
        {
            // Don't throw - audit logging should not break the main operation
            _logger.LogError(ex, "Failed to log audit event: {Action} for document {DocumentId}", action, documentId);
        }
    }

    public Task LogAsync(string action, int? documentId, string? documentName, int userId, string userName,
                               string? ipAddress = null, string? userAgent = null, string? details = null,
                               bool success = true, string? errorMessage = null)
    {
        return Task.Run(() => Log(action, documentId, documentName, userId, userName, ipAddress, userAgent, details, success, errorMessage));
    }

    public void Log(string action, int? documentId, string? documentName)
    {
        var userId = _userContext.GetCurrentUserId();
        var userName = _userContext.GetCurrentUserName();
        var ipAddress = _userContext.GetIpAddress();
        var userAgent = _userContext.GetUserAgent();

        Log(action, documentId, documentName, userId, userName, ipAddress, userAgent);
    }

    public Task LogAsync(string action, int? documentId, string? documentName)
    {
        return Task.Run(() => Log(action, documentId, documentName));
    }

    public IEnumerable<AuditLog> GetLogs(int? documentId = null, int? userId = null,
                                                          string? action = null, DateTime? fromDate = null,
                                                          DateTime? toDate = null, int limit = 100)
    {
        var logs = new List<AuditLog>();

        try
        {
            var query = "SELECT TOP (@Limit) * FROM AuditLogs WHERE 1=1";
            
            if (documentId.HasValue) query += " AND DocumentId = @DocumentId";
            if (userId.HasValue) query += " AND UserId = @UserId";
            if (!string.IsNullOrEmpty(action)) query += " AND Action = @Action";
            if (fromDate.HasValue) query += " AND Timestamp >= @FromDate";
            if (toDate.HasValue) query += " AND Timestamp <= @ToDate";
            
            query += " ORDER BY Timestamp DESC";

            DbCommand command = _db.GetSqlStringCommand(query);
            _db.AddInParameter(command, "@Limit", DbType.Int32, limit);
            if (documentId.HasValue) _db.AddInParameter(command, "@DocumentId", DbType.Int32, documentId.Value);
            if (userId.HasValue) _db.AddInParameter(command, "@UserId", DbType.Int32, userId.Value);
            if (!string.IsNullOrEmpty(action)) _db.AddInParameter(command, "@Action", DbType.String, action);
            if (fromDate.HasValue) _db.AddInParameter(command, "@FromDate", DbType.DateTime, fromDate.Value);
            if (toDate.HasValue) _db.AddInParameter(command, "@ToDate", DbType.DateTime, toDate.Value);

            using var reader = _db.ExecuteReader(command);
            while (reader.Read())
            {
                logs.Add(MapAuditLog(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs");
        }

        return logs;
    }

    public Task<IEnumerable<AuditLog>> GetLogsAsync(int? documentId = null, int? userId = null,
                                                          string? action = null, DateTime? fromDate = null,
                                                          DateTime? toDate = null, int limit = 100)
    {
        return Task.Run(() => GetLogs(documentId, userId, action, fromDate, toDate, limit));
    }

    public IEnumerable<AuditLog> GetDocumentHistory(int documentId, int limit = 50)
    {
        return GetLogs(documentId: documentId, limit: limit);
    }

    public Task<IEnumerable<AuditLog>> GetDocumentHistoryAsync(int documentId, int limit = 50)
    {
        return Task.Run(() => GetDocumentHistory(documentId, limit));
    }

    public IEnumerable<AuditLog> GetUserActivity(int userId, int limit = 50)
    {
        return GetLogs(userId: userId, limit: limit);
    }

    public Task<IEnumerable<AuditLog>> GetUserActivityAsync(int userId, int limit = 50)
    {
        return Task.Run(() => GetUserActivity(userId, limit));
    }

    public IEnumerable<AuditLog> GetRecentActivity(int limit = 100)
    {
        return GetLogs(limit: limit);
    }

    public Task<IEnumerable<AuditLog>> GetRecentActivityAsync(int limit = 100)
    {
        return Task.Run(() => GetRecentActivity(limit));
    }

    public AuditStats GetStats(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var stats = new AuditStats();

        try
        {
            var dateFilter = "";
            if (fromDate.HasValue) dateFilter += " AND Timestamp >= @FromDate";
            if (toDate.HasValue) dateFilter += " AND Timestamp <= @ToDate";

            var query = $@"
                SELECT 
                    COUNT(*) as TotalActions,
                    SUM(CASE WHEN Action = 'View' THEN 1 ELSE 0 END) as ViewCount,
                    SUM(CASE WHEN Action = 'Download' THEN 1 ELSE 0 END) as DownloadCount,
                    SUM(CASE WHEN Action = 'Create' THEN 1 ELSE 0 END) as CreateCount,
                    SUM(CASE WHEN Action = 'Update' THEN 1 ELSE 0 END) as UpdateCount,
                    SUM(CASE WHEN Action = 'Delete' THEN 1 ELSE 0 END) as DeleteCount,
                    SUM(CASE WHEN Success = 0 THEN 1 ELSE 0 END) as FailedActions,
                    COUNT(DISTINCT UserId) as UniqueUsers,
                    COUNT(DISTINCT DocumentId) as UniqueDocuments
                FROM AuditLogs WHERE 1=1 {dateFilter}";

            DbCommand command = _db.GetSqlStringCommand(query);
            if (fromDate.HasValue) _db.AddInParameter(command, "@FromDate", DbType.DateTime, fromDate.Value);
            if (toDate.HasValue) _db.AddInParameter(command, "@ToDate", DbType.DateTime, toDate.Value);

            using var reader = _db.ExecuteReader(command);
            if (reader.Read())
            {
                stats.TotalActions = reader["TotalActions"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalActions"]);
                stats.ViewCount = reader["ViewCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ViewCount"]);
                stats.DownloadCount = reader["DownloadCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DownloadCount"]);
                stats.CreateCount = reader["CreateCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CreateCount"]);
                stats.UpdateCount = reader["UpdateCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["UpdateCount"]);
                stats.DeleteCount = reader["DeleteCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DeleteCount"]);
                stats.FailedActions = reader["FailedActions"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FailedActions"]);
                stats.UniqueUsers = reader["UniqueUsers"] == DBNull.Value ? 0 : Convert.ToInt32(reader["UniqueUsers"]);
                stats.UniqueDocuments = reader["UniqueDocuments"] == DBNull.Value ? 0 : Convert.ToInt32(reader["UniqueDocuments"]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit stats");
        }

        return stats;
    }

    public Task<AuditStats> GetStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        return Task.Run(() => GetStats(fromDate, toDate));
    }

    private static AuditLog MapAuditLog(IDataReader reader)
    {
        return new AuditLog
        {
            AuditId = Convert.ToInt32(reader["AuditId"]),
            Action = reader["Action"] == DBNull.Value ? "" : Convert.ToString(reader["Action"]) ?? "",
            DocumentId = reader["DocumentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["DocumentId"]),
            DocumentName = reader["DocumentName"] == DBNull.Value ? null : Convert.ToString(reader["DocumentName"]),
            UserId = Convert.ToInt32(reader["UserId"]),
            UserName = reader["UserName"] == DBNull.Value ? "" : Convert.ToString(reader["UserName"]) ?? "",
            IpAddress = reader["IpAddress"] == DBNull.Value ? null : Convert.ToString(reader["IpAddress"]),
            UserAgent = reader["UserAgent"] == DBNull.Value ? null : Convert.ToString(reader["UserAgent"]),
            Timestamp = Convert.ToDateTime(reader["Timestamp"]),
            Details = reader["Details"] == DBNull.Value ? null : Convert.ToString(reader["Details"]),
            Success = Convert.ToBoolean(reader["Success"]),
            ErrorMessage = reader["ErrorMessage"] == DBNull.Value ? null : Convert.ToString(reader["ErrorMessage"])
        };
    }
}
