using DocumentManagementSystem.Models;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Service for managing document comments.
/// Maps to the legacy 'tblComment' table.
/// All mutating operations return iFishResponse (legacy pattern).
/// </summary>
public interface ICommentService
{
    /// <summary>Get all comments for a specific document.</summary>
    Task<IEnumerable<Comments>> GetByDocumentIdAsync(long documentId);

    /// <summary>Add a new comment to a document. Returns iFishResponse with RecordID = new comment ID.</summary>
    Task<iFishResponse> AddAsync(Comments comment);

    /// <summary>Delete a comment by its ID. Returns iFishResponse.</summary>
    Task<iFishResponse> DeleteAsync(long commentId);

    /// <summary>Get comment count for a document.</summary>
    Task<int> GetCountAsync(long documentId);
}
