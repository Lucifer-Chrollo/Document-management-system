namespace DocumentManagementSystem.Models;

/// <summary>
/// Represents an audit log entry for tracking document actions
/// </summary>
public class AuditLog
{
    public int AuditId { get; set; }
    
    /// <summary>
    /// Type of action: View, Download, Create, Update, Delete, Restore, Share
    /// </summary>
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the document affected (nullable for non-document actions)
    /// </summary>
    public int? DocumentId { get; set; }
    
    /// <summary>
    /// Name of the document at time of action
    /// </summary>
    public string? DocumentName { get; set; }
    
    /// <summary>
    /// User ID who performed the action
    /// </summary>
    public int UserId { get; set; }
    
    /// <summary>
    /// Username at time of action
    /// </summary>
    public string UserName { get; set; } = string.Empty;
    
    /// <summary>
    /// IP address of the user
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User agent/browser info
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Timestamp of the action (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Additional details in JSON format (old values, new values, etc.)
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// Was the action successful?
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Error message if action failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Common audit action types
/// </summary>
public static class AuditAction
{
    public const string View = "View";
    public const string Download = "Download";
    public const string Preview = "Preview";
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Restore = "Restore";
    public const string PermanentDelete = "PermanentDelete";
    public const string Share = "Share";
    public const string VersionUpload = "VersionUpload";
    public const string VersionRestore = "VersionRestore";
    public const string Login = "Login";
    public const string Logout = "Logout";
    public const string LoginFailed = "LoginFailed";
}
