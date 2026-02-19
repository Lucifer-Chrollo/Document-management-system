namespace DocumentManagementSystem.Models;

/// <summary>
/// Represents a secure, time-limited upload session for QR-based mobile uploads
/// </summary>
public class UploadSession
{
    public int SessionId { get; set; }
    
    /// <summary>
    /// User who created this upload session
    /// </summary>
    public int UserId { get; set; }
    
    /// <summary>
    /// Cryptographic token (HMAC-signed) for URL
    /// </summary>
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// Hashed PIN for verification
    /// </summary>
    public string PinHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Session creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Session expiration timestamp
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Maximum failed PIN attempts allowed
    /// </summary>
    public int MaxAttempts { get; set; } = 3;
    
    /// <summary>
    /// Current failed PIN attempts
    /// </summary>
    public int FailedAttempts { get; set; } = 0;
    
    /// <summary>
    /// Maximum number of files allowed in this session
    /// </summary>
    public int MaxFiles { get; set; } = 10;
    
    /// <summary>
    /// Number of files uploaded in this session
    /// </summary>
    public int FilesUploaded { get; set; } = 0;
    
    /// <summary>
    /// Whether the session is still active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Whether PIN has been verified for this session
    /// </summary>
    public bool IsPinVerified { get; set; } = false;
    
    /// <summary>
    /// IP address that created the session
    /// </summary>
    public string? CreatorIpAddress { get; set; }
    
    /// <summary>
    /// IP address of mobile device using the session
    /// </summary>
    public string? UploaderIpAddress { get; set; }
    
    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }
    
    /// <summary>
    /// Optional category for uploaded files
    /// </summary>
    public int? DefaultCategoryId { get; set; }
    
    /// <summary>
    /// Session label/description set by user
    /// </summary>
    public string? Label { get; set; }
    
    // Navigation
    public string? UserName { get; set; }
    
    /// <summary>
    /// Check if session is valid for uploads
    /// </summary>
    public bool IsValid => IsActive 
                          && DateTime.UtcNow < ExpiresAt 
                          && FailedAttempts < MaxAttempts 
                          && FilesUploaded < MaxFiles;
    
    /// <summary>
    /// Get remaining time in minutes
    /// </summary>
    public int RemainingMinutes => Math.Max(0, (int)(ExpiresAt - DateTime.UtcNow).TotalMinutes);
}
