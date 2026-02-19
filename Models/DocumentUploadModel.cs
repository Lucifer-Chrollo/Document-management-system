using Microsoft.AspNetCore.Http;

namespace DocumentManagementSystem.Models;

public class DocumentUploadModel
{
    public IFormFile File { get; set; } = default!;
    public string? BatchLabel { get; set; }
    public int? CategoryId { get; set; }
    public string? Description { get; set; }
}
