namespace DocumentManagementSystem.Models;

public static class DocumentExtensions
{
    public static Document Clone(this Document doc)
    {
        return new Document
        {
            DocumentName = doc.DocumentName,
            CategoryID = doc.CategoryID,
            UploadedBy = doc.UploadedBy,
            SourcePath = doc.SourcePath,
            Extension = doc.Extension,
            Password = doc.Password,
            Status = doc.Status,
            DepartmentID = doc.DepartmentID,
            FileSize = doc.FileSize,
            FileType = doc.FileType
        };
    }
}
