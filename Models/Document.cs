using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentManagementSystem.Models;

// Matches DBBridge.Document structure
public class Document
{
    [Key]
    public int DocumentId { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string DocumentName { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? FileType { get; set; } // MimeType/Extension
    
    public int CategoryID { get; set; }
    
    public int UploadedBy { get; set; } // UserId as int
    
    [Required]
    [MaxLength(1000)]
    public string Path { get; set; } = string.Empty; // FilePath

    [MaxLength(1000)]
    public string? SourcePath { get; set; }
    
    [MaxLength(20)]
    public string? Extension { get; set; }
    
    [MaxLength(256)]
    public string? Password { get; set; }
    
    [MaxLength(50)]
    public string Status { get; set; } = "Active";
    
    public int ParentID { get; set; } = 0; // For folder structure if needed, or versioning
    
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    
    public int CurrentVersion { get; set; } = 1;
    
    [NotMapped] // Assuming these are for display/joins
    public string? CategoryName { get; set; }
    
    [NotMapped]
    public string? UserName { get; set; }
    
    
    
    public int DepartmentID { get; set; }
    public int LocationID { get; set; }
    
    [NotMapped]
    public string? DepartmentName { get; set; }

    [NotMapped]
    public string? LocationName { get; set; }

    public int UpdatedBy { get; set; }

    [NotMapped]
    public string? UpdatedByName { get; set; }
    
    // Additional fields needed for app logic but keeping base structure clean
    public long FileSize { get; set; }
    public string? FileHash { get; set; }
    
    // Duplicate detection
    [NotMapped]
    public bool IsDuplicate { get; set; }
    [NotMapped]
    public int? DuplicateOf { get; set; }
    
    public bool IsDeleted { get; set; }

    // OCR related fields
    public bool IsOcrProcessed { get; set; }
    public string? OcrText { get; set; }
    public decimal? OcrConfidence { get; set; }
    public string? OcrEngine { get; set; }
    public DateTime? OcrProcessedDate { get; set; }
    public bool HasExtractedText { get; set; }
    
    [MaxLength(50)]
    public string? CompressionAlgorithm { get; set; }
    
    public long? CompressedSize { get; set; }
    
    [MaxLength(200)]
    public string? BatchLabel { get; set; }

    // Navigation
    [ForeignKey("CategoryID")]
    public virtual Category? Category { get; set; }
    
    [ForeignKey("DepartmentID")]
    public virtual Department? Department { get; set; }

    public virtual List<UserDocumentRights> UserDocumentRightsList { get; set; } = new List<UserDocumentRights>();
    public virtual List<Comments> CommentsList { get; set; } = new List<Comments>(); // Renamed for clarity

    public Document Clone()
    {
        return new Document
        {
            CategoryID = this.CategoryID,
            DepartmentID = this.DepartmentID,
            LocationID = this.LocationID,
            Password = this.Password,
            Status = this.Status,
            UploadedBy = this.UploadedBy,
            UpdatedBy = this.UpdatedBy,
            BatchLabel = this.BatchLabel,
            UserDocumentRightsList = this.UserDocumentRightsList.Select(r => new UserDocumentRights
            {
                UserId = r.UserId,
                Rights = r.Rights,
                RightsName = r.RightsName,
                UserName = r.UserName
            }).ToList()
        };
    }
}

public class Comments
{
    [Key]
    public long ID { get; set; }

    public string? Comment { get; set; }
    public int? CommentBy { get; set; }
    public DateTime? CommentDate { get; set; }
    public long? DocumentID { get; set; }

    [NotMapped]
    public string? CommentByName { get; set; }
}

public class UserDocumentRights 
{
    [Key]
    public int RightId { get; set; }

    public int UserId { get; set; }
    public int DocumentId { get; set; }
    public int Rights { get; set; }
    public int? GroupId { get; set; } // Added GroupId

    [NotMapped]
    public string? RightsName { get; set; }

    [NotMapped]
    public string? UserName { get; set; }
    
    [ForeignKey("DocumentId")]
    public virtual Document? Document { get; set; }
}

public class ArchiveDateNode
{
    public string Text { get; set; } = string.Empty;
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    public List<ArchiveDateNode> Children { get; set; } = new();
}
