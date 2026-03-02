using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentManagementSystem.Models;

/// <summary>
/// Maps directly to the legacy 'HRM_Departments' table in iBusinessFlex.
/// </summary>
[Table("HRM_Departments")]
public class Department
{
    [Key]
    public int DepartmentID { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Description { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(50)]
    public string? SalaryExpenseAccountCode { get; set; }

    [MaxLength(50)]
    public string? EOBIExpenseAccountCode { get; set; }

    [MaxLength(50)]
    public string? PESSIExpenseAccountCode { get; set; }

    [MaxLength(50)]
    public string? CanteenExpenseAccountCode { get; set; }
}
