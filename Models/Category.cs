using System.ComponentModel.DataAnnotations;

namespace DocumentManagementSystem.Models;

// Updated Category to use int Key to match DBBridge FKs
public class Category
{
    [Key]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(200)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int CategoryOrder { get; set; } = 0;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;

    // Navigation
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
