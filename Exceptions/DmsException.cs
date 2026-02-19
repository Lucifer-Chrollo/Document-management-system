using System;

namespace DocumentManagementSystem.Exceptions;

/// <summary>
/// Base class for all domain-specific exceptions in the Document Management System.
/// </summary>
public class DmsException : Exception
{
    public DmsException(string message) : base(message) { }
    public DmsException(string message, Exception innerException) : base(message, innerException) { }
}
