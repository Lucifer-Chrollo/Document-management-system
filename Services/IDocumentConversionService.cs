using DocumentManagementSystem.Services;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Converts document formats (e.g., Word to PDF) for inline preview.
/// </summary>
public interface IDocumentConversionService
{
    /// <summary>
    /// Converts a Word document stream (.docx) to a PDF stream.
    /// </summary>
    Task<Stream?> ConvertWordToPdfAsync(Stream inputStream, string extension);
    
    /// <summary>
    /// Returns true if the given extension can be converted to PDF for preview.
    /// </summary>
    bool CanConvertToPreview(string extension);
}
