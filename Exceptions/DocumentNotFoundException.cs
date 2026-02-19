namespace DocumentManagementSystem.Exceptions;

/// <summary>
/// Thrown when a requested document cannot be found.
/// </summary>
public class DocumentNotFoundException : DmsException
{
    public int DocumentId { get; }

    public DocumentNotFoundException(int documentId) 
        : base($"Document with ID {documentId} was not found.")
    {
        DocumentId = documentId;
    }
}
