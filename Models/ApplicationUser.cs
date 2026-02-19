using Microsoft.AspNetCore.Identity;

namespace DocumentManagementSystem.Models;

/// <summary>
/// Extended user model with additional profile information
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }
    public long StorageQuotaBytes { get; set; } = 1073741824; // 1 GB default
    public long StorageUsedBytes { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    
    // MFA Properties
    public string? MfaSecret { get; set; }
    public string? MfaRecoveryCode { get; set; }
    public DateTime? MfaSetupDate { get; set; }

    // Navigation properties
    public virtual ICollection<Document> OwnedDocuments { get; set; } = new List<Document>();
}
