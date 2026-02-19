using DocumentManagementSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Security.Claims;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Audit trail service for logging document actions
/// </summary>
public class AuditService : IAuditService
{
    private readonly string _connectionString;
    private readonly ILogger<AuditService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    #region SQL Queries

    private const string SQL_INSERT_AUDIT_LOG = @"
        INSERT INTO AuditLogs (Action, DocumentId, DocumentName, UserId, UserName, IpAddress, UserAgent, Timestamp, Details, Success, ErrorMessage)
        VALUES (@Action, @DocumentId, @DocumentName, @UserId, @UserName, @IpAddress, @UserAgent, @Timestamp, @Details, @Success, @ErrorMessage)";

    #endregion

    public AuditService(IConfiguration configuration, ILogger<AuditService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not configured");
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Log(string action, int? documentId, string? documentName, int userId, string userName,
                               string? ipAddress = null, string? userAgent = null, string? details = null,
                               bool success = true, string? errorMessage = null)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(SQL_INSERT_AUDIT_LOG, connection);
            command.Parameters.Add("@Action", SqlDbType.NVarChar).Value = action;
            command.Parameters.Add("@DocumentId", SqlDbType.Int).Value = (object?)documentId ?? DBNull.Value;
            command.Parameters.Add("@DocumentName", SqlDbType.NVarChar).Value = (object?)documentName ?? DBNull.Value;
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            command.Parameters.Add("@UserName", SqlDbType.NVarChar).Value = userName;
            command.Parameters.Add("@IpAddress", SqlDbType.NVarChar).Value = (object?)ipAddress ?? DBNull.Value;
            command.Parameters.Add("@UserAgent", SqlDbType.NVarChar).Value = (object?)userAgent ?? DBNull.Value;
            command.Parameters.Add("@Timestamp", SqlDbType.DateTime).Value = DateTime.UtcNow;
            command.Parameters.Add("@Details", SqlDbType.NVarChar).Value = (object?)details ?? DBNull.Value;
            command.Parameters.Add("@Success", SqlDbType.Bit).Value = success;
            command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar).Value = (object?)errorMessage ?? DBNull.Value;

            command.ExecuteNonQuery();
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
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = 0;
        var userName = "System";
        string? ipAddress = null;
        string? userAgent = null;

        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var uid)) userId = uid;
            userName = httpContext.User.Identity?.Name ?? "Unknown";
            ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            userAgent = httpContext.Request.Headers["User-Agent"].FirstOrDefault();
        }

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
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var query = "SELECT TOP (@Limit) * FROM AuditLogs WHERE 1=1";
            
            if (documentId.HasValue) query += " AND DocumentId = @DocumentId";
            if (userId.HasValue) query += " AND UserId = @UserId";
            if (!string.IsNullOrEmpty(action)) query += " AND Action = @Action";
            if (fromDate.HasValue) query += " AND Timestamp >= @FromDate";
            if (toDate.HasValue) query += " AND Timestamp <= @ToDate";
            
            query += " ORDER BY Timestamp DESC";

            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@Limit", SqlDbType.Int).Value = limit;
            if (documentId.HasValue) command.Parameters.Add("@DocumentId", SqlDbType.Int).Value = documentId.Value;
            if (userId.HasValue) command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId.Value;
            if (!string.IsNullOrEmpty(action)) command.Parameters.Add("@Action", SqlDbType.NVarChar).Value = action;
            if (fromDate.HasValue) command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = fromDate.Value;
            if (toDate.HasValue) command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = toDate.Value;

            using var reader = command.ExecuteReader();
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
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

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

            using var command = new SqlCommand(query, connection);
            if (fromDate.HasValue) command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = fromDate.Value;
            if (toDate.HasValue) command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = toDate.Value;

            using var reader = command.ExecuteReader();
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

    private static AuditLog MapAuditLog(SqlDataReader reader)
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
