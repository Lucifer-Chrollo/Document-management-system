using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentManagementSystem.Models;

public class DocumentVersion
{
    [Key]
    public int VersionId { get; set; }

    public int DocumentId { get; set; }

    public int VersionNumber { get; set; }

    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public int CreatedBy { get; set; }

    [ForeignKey("DocumentId")]
    public virtual Document? Document { get; set; }
}
