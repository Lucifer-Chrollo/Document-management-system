namespace DocumentManagementSystem.Exceptions;

/// <summary>
/// Thrown when an operation would exceed the allocated storage quota.
/// </summary>
public class StorageQuotaExceededException : DmsException
{
    public long RequestedBytes { get; }
    public long? RemainingBytes { get; }

    public StorageQuotaExceededException(string message, long requestedBytes, long? remainingBytes = null) 
        : base(message)
    {
        RequestedBytes = requestedBytes;
        RemainingBytes = remainingBytes;
    }
}
