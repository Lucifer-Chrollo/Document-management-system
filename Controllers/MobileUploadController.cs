using Microsoft.AspNetCore.Mvc;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;

namespace DocumentManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class MobileUploadController : ControllerBase
    {
        private readonly IUploadSessionService _uploadSessionService;
        private readonly IDocumentService _documentService;

        public MobileUploadController(
            IUploadSessionService uploadSessionService,
            IDocumentService documentService)
        {
            _uploadSessionService = uploadSessionService;
            _documentService = documentService;
        }

        [HttpPost("upload/{token}")]
        [RequestSizeLimit(500 * 1024 * 1024)] // 500MB total
        [RequestFormLimits(MultipartBodyLengthLimit = 500 * 1024 * 1024)]
        public async Task<IActionResult> Upload(string token, [FromForm] List<IFormFile> files)
        {
            var session = await _uploadSessionService.GetByTokenAsync(token);
            if (session == null || !session.IsValid || !session.IsPinVerified)
            {
                return BadRequest(new { success = false, message = "Invalid or expired session." });
            }

            var results = new List<object>();
            int successCount = 0;

            foreach (var file in files)
            {
                try
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    ms.Position = 0;

                    var document = new Document
                    {
                        DocumentName = file.FileName,
                        FileType = file.ContentType,
                        CategoryID = session.DefaultCategoryId ?? 1,
                        UploadedBy = session.UserId,
                        Extension = Path.GetExtension(file.FileName),
                        FileSize = file.Length,
                        Status = "Active"
                    };

                    var response = await _documentService.CreateAsync(document, ms);
                    if (response.Result)
                    {
                        await _uploadSessionService.IncrementFileCountAsync(token);
                        successCount++;
                        results.Add(new { name = file.FileName, success = true });
                    }
                    else
                    {
                        results.Add(new { name = file.FileName, success = false, error = response.Message });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new { name = file.FileName, success = false, error = ex.Message });
                }
            }

            return Ok(new { success = successCount > 0, uploaded = successCount, total = files.Count, results });
        }
    }
}
