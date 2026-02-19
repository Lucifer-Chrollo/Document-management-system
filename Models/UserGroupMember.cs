using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentManagementSystem.Models;

/// <summary>
/// A new link table to manage User <-> Group membership.
/// This table does not exist in the legacy schema and is a new addition for the modern system.
/// </summary>
public class UserGroupMember
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    public int GroupId { get; set; }

    [ForeignKey("UserId")]
    public virtual DmsUser? User { get; set; }

    [ForeignKey("GroupId")]
    public virtual UserGroup? Group { get; set; }

    // Display fields for UI
    [NotMapped]
    public string? UserName { get; set; }
    [NotMapped]
    public string? Email { get; set; }
    [NotMapped]
    public string? Role { get; set; } = "Member";
    [NotMapped]
    public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
}
