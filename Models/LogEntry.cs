using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentManagementSystem.Models;

/// <summary>
/// Maps directly to the legacy 'iFF_Logs' table.
/// Matches the legacy DBBridge.LogEntry and UserLogEntry models.
/// </summary>
public class LogEntry
{
    [Key]
    public long LogID { get; set; } // bigint IDENTITY

    [Required]
    [MaxLength(50)]
    public string OperationType { get; set; } = string.Empty; // e.g., 'ADD', 'DELETE', 'UPDATE'

    public long OperationId { get; set; } // The ID of the item being operated on (e.g., DocumentID)

    public int UserId { get; set; } // The user performing the action

    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(255)]
    public string? SerialName { get; set; } // Often used for DocumentName or Description

    [MaxLength(50)]
    public string? ClientIP { get; set; } // Matches legacy 'ClientIP' field

    [MaxLength(50)]
    public string? SystemIP { get; set; } // Alias for compatibility with UserLogEntry

    /// <summary>
    /// JSON metadata for the operation. Matches legacy 'OperationData' field.
    /// </summary>
    public string? OperationData { get; set; }

    // Display-only fields (populated via JOIN with Users table)
    [NotMapped]
    public string? FName { get; set; }

    [NotMapped]
    public string? LName { get; set; }
}
