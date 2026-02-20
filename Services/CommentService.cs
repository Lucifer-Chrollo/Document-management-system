using DocumentManagementSystem.Helpers;
using DocumentManagementSystem.Models;
using Microsoft.Practices.EnterpriseLibrary.Data;
using System.Data;
using System.Data.Common;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Enterprise Library implementation of the Comment Service.
/// Maps to the legacy 'tblComment' table in iBusinessFlex.
/// All mutating operations return iFishResponse (legacy pattern).
/// </summary>
public class CommentService : ICommentService
{
    private readonly Database _db;
    private readonly ILogger<CommentService> _logger;

    private const string SQL_SELECT_BY_DOC = @"
        SELECT c.ID, c.Comment, c.CommentBy, c.CommentDate, c.DocumentID,
               u.FirstName + ' ' + ISNULL(u.LastName, '') AS CommentByName
        FROM tblComment c
        LEFT JOIN Users u ON c.CommentBy = u.Id
        WHERE c.DocumentID = @DocumentID
        ORDER BY c.CommentDate DESC";

    private const string SQL_INSERT = @"
        INSERT INTO tblComment (Comment, CommentBy, CommentDate, DocumentID)
        VALUES (@Comment, @CommentBy, @CommentDate, @DocumentID);
        SELECT CAST(SCOPE_IDENTITY() AS bigint);";

    private const string SQL_DELETE = "DELETE FROM tblComment WHERE ID = @ID";

    private const string SQL_COUNT = "SELECT COUNT(*) FROM tblComment WHERE DocumentID = @DocumentID";

    public CommentService(Database db, ILogger<CommentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all comments for a given document, with commenter names from a LEFT JOIN.
    /// </summary>
    public async Task<IEnumerable<Comments>> GetByDocumentIdAsync(long documentId)
    {
        try
        {
            return ResiliencyPolicies.GetSqlRetryPolicy(_logger, "GetCommentsByDoc").Execute(() =>
            {
                var comments = new List<Comments>();
                DbCommand command = _db.GetSqlStringCommand(SQL_SELECT_BY_DOC);
                _db.AddInParameter(command, "@DocumentID", DbType.Int64, documentId);

                using (IDataReader reader = _db.ExecuteReader(command))
                {
                    while (reader.Read())
                    {
                        comments.Add(MapComment(reader));
                    }
                }
                return (IEnumerable<Comments>)comments;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments for document {DocumentId}", documentId);
            return Enumerable.Empty<Comments>();
        }
    }

    /// <summary>
    /// Adds a new comment. Returns iFishResponse with RecordID = new comment ID.
    /// </summary>
    public async Task<iFishResponse> AddAsync(Comments comment)
    {
        var response = new iFishResponse();
        try
        {
            ResiliencyPolicies.GetSqlRetryPolicy(_logger, "AddComment").Execute(() =>
            {
                DbCommand command = _db.GetSqlStringCommand(SQL_INSERT);
                _db.AddInParameter(command, "@Comment", DbType.String, (object?)comment.Comment ?? DBNull.Value);
                _db.AddInParameter(command, "@CommentBy", DbType.Int32, (object?)comment.CommentBy ?? DBNull.Value);
                _db.AddInParameter(command, "@CommentDate", DbType.DateTime, comment.CommentDate ?? DateTime.UtcNow);
                _db.AddInParameter(command, "@DocumentID", DbType.Int64, (object?)comment.DocumentID ?? DBNull.Value);

                var newId = _db.ExecuteScalar(command);
                if (newId != null)
                {
                    comment.ID = (long)newId;
                }
            });

            response.Result = true;
            response.RecordID = comment.ID;
            response.Message = "Comment added successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment for document {DocumentId}", comment.DocumentID);
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    /// <summary>
    /// Permanently deletes a comment by its ID. Returns iFishResponse.
    /// </summary>
    public async Task<iFishResponse> DeleteAsync(long commentId)
    {
        var response = new iFishResponse();
        try
        {
            ResiliencyPolicies.GetSqlRetryPolicy(_logger, "DeleteComment").Execute(() =>
            {
                DbCommand command = _db.GetSqlStringCommand(SQL_DELETE);
                _db.AddInParameter(command, "@ID", DbType.Int64, commentId);
                _db.ExecuteNonQuery(command);
            });

            response.Result = true;
            response.RecordID = commentId;
            response.Message = "Comment deleted successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment {CommentId}", commentId);
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    /// <summary>
    /// Gets the total comment count for a document.
    /// </summary>
    public async Task<int> GetCountAsync(long documentId)
    {
        try
        {
            return ResiliencyPolicies.GetSqlRetryPolicy(_logger, "GetCommentCount").Execute(() =>
            {
                DbCommand command = _db.GetSqlStringCommand(SQL_COUNT);
                _db.AddInParameter(command, "@DocumentID", DbType.Int64, documentId);
                return Convert.ToInt32(_db.ExecuteScalar(command));
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comment count for document {DocumentId}", documentId);
            return 0;
        }
    }

    private Comments MapComment(IDataReader reader)
    {
        return new Comments
        {
            ID = reader.GetInt64Safe("ID"),
            Comment = reader.GetStringNullable("Comment"),
            CommentBy = reader["CommentBy"] == DBNull.Value ? null : Convert.ToInt32(reader["CommentBy"]),
            CommentDate = reader["CommentDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["CommentDate"]),
            DocumentID = reader["DocumentID"] == DBNull.Value ? null : Convert.ToInt64(reader["DocumentID"]),
            CommentByName = reader.GetStringNullable("CommentByName")
        };
    }
}
