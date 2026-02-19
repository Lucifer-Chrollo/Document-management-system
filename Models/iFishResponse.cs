namespace DocumentManagementSystem.Models;

/// <summary>
/// Standard API response wrapper, matching the legacy iBusinessFlex 'iFishResponse' pattern.
/// Used by all API controllers for consistent response format.
/// </summary>
public class iFishResponse
{
    public iFishResponse()
    {
        Result = false;
        ReturnCode = 0;
        Message = "";
        RecordID = 0;
    }

    /// <summary>Whether the operation was successful.</summary>
    public bool Result { get; set; }

    /// <summary>Numerical return code (0 = default, positive = success, negative = error).</summary>
    public long ReturnCode { get; set; }

    /// <summary>Human-readable message for the client.</summary>
    public string Message { get; set; }

    /// <summary>The ID of the created/modified record.</summary>
    public long RecordID { get; set; }
}
