namespace DocumentManagementSystem.Exceptions;

/// <summary>
/// Thrown when a user attempts to access a resource they do not have permission for.
/// </summary>
public class AccessDeniedException : DmsException
{
    public string ResourceType { get; }
    public string ResourceId { get; }
    public int? UserId { get; }

    public AccessDeniedException(string message, string resourceType, string resourceId, int? userId = null) 
        : base(message)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
        UserId = userId;
    }
}
