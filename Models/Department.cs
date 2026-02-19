using System.ComponentModel.DataAnnotations;

namespace DocumentManagementSystem.Models;

// Updated Department to use int Key to match DBBridge FKs
public class Department
{
    [Key]
    public int DepartmentId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DepartmentName { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}


