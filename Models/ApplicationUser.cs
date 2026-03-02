using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentManagementSystem.Models;

/// <summary>
/// Extended user model with additional profile information
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Department { get; set; } // Legacy text column, prefer DepartmentId FK
    public int? DepartmentId { get; set; } // FK to Departments table
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }
    public long StorageQuotaBytes { get; set; } = 1073741824; // 1 GB default
    public long StorageUsedBytes { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// AES-encrypted copy of user's password for admin viewing.
    /// </summary>
    public string? EncryptedPassword { get; set; }
    
    // MFA Properties
    public string? MfaSecret { get; set; }
    public string? MfaRecoveryCode { get; set; }
    public DateTime? MfaSetupDate { get; set; }

    // Navigation properties
    public virtual ICollection<Document> OwnedDocuments { get; set; } = new List<Document>();

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}".Trim();
}
