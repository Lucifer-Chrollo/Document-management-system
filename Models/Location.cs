using System.ComponentModel.DataAnnotations;

namespace DocumentManagementSystem.Models;

public class Location
{
    [Key]
    public int LocationId { get; set; }

    [Required]
    [MaxLength(200)]
    public string LocationName { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
