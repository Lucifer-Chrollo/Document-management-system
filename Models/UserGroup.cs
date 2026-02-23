using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentManagementSystem.Models;

/// <summary>
/// Maps to the 'UserGroups' table found in the SQL script.
/// </summary>
public class UserGroup
{
    [Key]
    public int GroupId { get; set; }

    [Required]
    [MaxLength(100)]
    public string GroupName { get; set; } = string.Empty;

    public int CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Permissions as bitmask: Read=1, Write=2, Delete=4
    public int DefaultRights { get; set; }

    // Display fields (not mapped to DB if using simple Dapper join, or filled via separate query)
    [NotMapped]
    public string? CreatorName { get; set; }
    [NotMapped]
    public int MemberCount { get; set; }
}
