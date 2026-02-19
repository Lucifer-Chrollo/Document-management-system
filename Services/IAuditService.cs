using DocumentManagementSystem.Models;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Service for logging audit trail entries
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Log an audit event (Synchronous)
    /// </summary>
    void Log(string action, int? documentId, string? documentName, int userId, string userName, 
                  string? ipAddress = null, string? userAgent = null, string? details = null, 
                  bool success = true, string? errorMessage = null);

    /// <summary>
    /// Log an audit event
    /// </summary>
    Task LogAsync(string action, int? documentId, string? documentName, int userId, string userName, 
                  string? ipAddress = null, string? userAgent = null, string? details = null, 
                  bool success = true, string? errorMessage = null);
    
    /// <summary>
    /// Log an audit event with minimal parameters (Synchronous)
    /// </summary>
    void Log(string action, int? documentId, string? documentName);

    /// <summary>
    /// Log an audit event with minimal parameters
    /// </summary>
    Task LogAsync(string action, int? documentId, string? documentName);
    
    /// <summary>
    /// Get audit logs with optional filtering (Synchronous)
    /// </summary>
    IEnumerable<AuditLog> GetLogs(int? documentId = null, int? userId = null, 
                                              string? action = null, DateTime? fromDate = null, 
                                              DateTime? toDate = null, int limit = 100);

    /// <summary>
    /// Get audit logs with optional filtering
    /// </summary>
    Task<IEnumerable<AuditLog>> GetLogsAsync(int? documentId = null, int? userId = null, 
                                              string? action = null, DateTime? fromDate = null, 
                                              DateTime? toDate = null, int limit = 100);
    
    /// <summary>
    /// Get logs for a specific document (Synchronous)
    /// </summary>
    IEnumerable<AuditLog> GetDocumentHistory(int documentId, int limit = 50);

    /// <summary>
    /// Get logs for a specific document
    /// </summary>
    Task<IEnumerable<AuditLog>> GetDocumentHistoryAsync(int documentId, int limit = 50);
    
    /// <summary>
    /// Get logs for a specific user (Synchronous)
    /// </summary>
    IEnumerable<AuditLog> GetUserActivity(int userId, int limit = 50);

    /// <summary>
    /// Get logs for a specific user
    /// </summary>
    Task<IEnumerable<AuditLog>> GetUserActivityAsync(int userId, int limit = 50);
    
    /// <summary>
    /// Get recent activity across all documents (Synchronous)
    /// </summary>
    IEnumerable<AuditLog> GetRecentActivity(int limit = 100);

    /// <summary>
    /// Get recent activity across all documents
    /// </summary>
    Task<IEnumerable<AuditLog>> GetRecentActivityAsync(int limit = 100);
    
    /// <summary>
    /// Get audit statistics (Synchronous)
    /// </summary>
    AuditStats GetStats(DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Get audit statistics
    /// </summary>
    Task<AuditStats> GetStatsAsync(DateTime? fromDate = null, DateTime? toDate = null);
}

/// <summary>
/// Audit statistics
/// </summary>
public class AuditStats
{
    public int TotalActions { get; set; }
    public int ViewCount { get; set; }
    public int DownloadCount { get; set; }
    public int CreateCount { get; set; }
    public int UpdateCount { get; set; }
    public int DeleteCount { get; set; }
    public int FailedActions { get; set; }
    public int UniqueUsers { get; set; }
    public int UniqueDocuments { get; set; }
}
