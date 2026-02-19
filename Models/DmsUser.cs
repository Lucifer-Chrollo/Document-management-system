using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentManagementSystem.Models;

/// <summary>
/// Maps directly to the legacy 'Users' table in iBusinessFlex.
/// Used by services that need to query the original user schema (UserGroupService, etc.)
/// This is separate from ApplicationUser which is used by ASP.NET Core Identity.
/// </summary>
public class DmsUser
{
    [Key]
    public int UserID { get; set; }

    [Required]
    [MaxLength(150)]
    public string FName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? LName { get; set; }

    [Required]
    [MaxLength(100)]
    public string LoginName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Email { get; set; }

    [Required]
    [MaxLength(150)]
    public string Password { get; set; } = string.Empty;

    public bool Status { get; set; } = true; // bit DEFAULT 1

    [MaxLength(500)]
    public string? Description { get; set; }

    public int? LanguageId { get; set; }

    public int? DepartmentID { get; set; }

    public string? UserGroup { get; set; } // nvarchar(max) - comma-separated group IDs

    public DateTime? CreationDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime? LastLoginTime { get; set; }

    [MaxLength(50)]
    public string? LoginStatus { get; set; }

    public int? BranchID { get; set; }

    [NotMapped]
    public string FullName => $"{FName} {LName}".Trim();
}
