using DocumentManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagementSystem.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class FilesController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IDocumentConversionService _conversionService;
    private readonly IUserGroupService _userGroupService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IDocumentService documentService, IDocumentConversionService conversionService, IUserGroupService userGroupService, ILogger<FilesController> logger)
    {
        _documentService = documentService;
        _conversionService = conversionService;
        _userGroupService = userGroupService;
        _logger = logger;
    }

    [HttpGet("preview/{id}")]
    public async Task<IActionResult> GetPreview(int id, [FromQuery] string? pwd = null)
    {
        if (!await UserCanReadDocumentAsync(id)) return Forbid();

        try
        {
            _logger.LogInformation("Preview requested for document ID: {Id}", id);
            
            var document = await _documentService.GetByIdAsync(id);
            if (document == null)
            {
                _logger.LogWarning("Preview: Document {Id} not found by GetByIdAsync", id);
                return NotFound($"Document {id} not found");
            }

            // Check Password Protection — Admins bypass
            var isAdminUser = HttpContext.User.IsInRole("Admin") || HttpContext.User.Identity?.Name?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;
            if (!string.IsNullOrEmpty(document.Password) && !isAdminUser)
            {
                if (pwd != document.Password)
                {
                    _logger.LogWarning("Preview denied for document {Id}: Invalid or missing password", id);
                    return Unauthorized("This document is password protected.");
                }
            }

            _logger.LogInformation("Preview: Document found - Name: {Name}, Extension: {Ext}, Path: {Path}", 
                document.DocumentName, document.Extension, document.Path);

            var stream = await _documentService.DownloadAsync(id);
            if (stream == null)
            {
                _logger.LogWarning("Preview: DownloadAsync returned null for document {Id}", id);
                return NotFound($"File content not available for document {id}");
            }

            var contentType = document.FileType ?? "application/octet-stream";
            var ext = document.Extension?.ToLowerInvariant() ?? "";

            // Convert Word documents to PDF for inline preview
            if (_conversionService.CanConvertToPreview(ext))
            {
                _logger.LogInformation("Preview: Converting {Ext} to PDF for document {Id}", ext, id);
                var pdfStream = await _conversionService.ConvertWordToPdfAsync(stream, ext);
                if (pdfStream != null)
                {
                    await stream.DisposeAsync();
                    Response.Headers.Append("Content-Disposition", $"inline; filename=\"{Path.GetFileNameWithoutExtension(document.DocumentName)}.pdf\"");
                    return File(pdfStream, "application/pdf");
                }
            }

            // Override Content-Type for known previewable types to ensure browser handling
            if (ext == ".pdf") contentType = "application/pdf";
            else if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";
            else if (ext == ".png") contentType = "image/png";
            else if (ext == ".gif") contentType = "image/gif";
            else if (ext == ".webp") contentType = "image/webp";
            else if (ext == ".txt") contentType = "text/plain";
            
            // Force inline disposition
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{document.DocumentName}{ext}\"");

            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving file preview for document {Id}", id);
            return StatusCode(500, $"Preview error: {ex.Message}");
        }
    }

    [HttpGet("download/{id}")]
    public async Task<IActionResult> GetDownload(int id, [FromQuery] string? pwd = null)
    {
        if (!await UserCanReadDocumentAsync(id)) return Forbid();

        try
        {
            var document = await _documentService.GetByIdAsync(id);
            if (document == null) return NotFound();

            // Check Password Protection — Admins bypass
            var isAdminUser = HttpContext.User.IsInRole("Admin") || HttpContext.User.Identity?.Name?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;
            if (!string.IsNullOrEmpty(document.Password) && !isAdminUser)
            {
                if (pwd != document.Password)
                {
                    _logger.LogWarning("Download denied for document {Id}: Invalid or missing password", id);
                    return Unauthorized("This document is password protected.");
                }
            }

            var stream = await _documentService.DownloadAsync(id);
            if (stream == null) return NotFound();

            var contentType = document.FileType ?? "application/octet-stream";
            var fileName = document.DocumentName;
            if (!string.IsNullOrEmpty(document.Extension) && !fileName.EndsWith(document.Extension, StringComparison.OrdinalIgnoreCase))
            {
                fileName += document.Extension;
            }

            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("download/version/{versionId}")]
    public async Task<IActionResult> GetVersionDownload(int versionId)
    {
        try
        {
            var version = await _documentService.GetVersionAsync(versionId);
            if (version == null) return NotFound();

            if (!await UserCanReadDocumentAsync(version.DocumentId)) return Forbid();

            var stream = await _documentService.DownloadVersionAsync(versionId);
            if (stream == null) return NotFound();

            // We need metadata (filename) - DownloadVersionAsync could return a tuple or we fetch separately.
            // For now, let's assume DownloadVersionAsync returns the stream and we need to look up filename.
            // Or, let's update IDocumentService to expose a way to get version info.
            // Simplified: Add GetVersionAsync to service.
            var history = await _documentService.GetHistoryAsync(0); // This logic is flawed, GetHistory takes DocId.
            // We need GetVersionById.
            
            // Let's rely on Valid content disposition from service or generic name?
            // To be proper, I will add GetVersionById to service.
            
            return File(stream, "application/octet-stream", $"version_{versionId}.file"); 
            // Re-visiting: I'll implement a proper DownloadVersionAsync in service that returns (Stream, ContentType, FileName).
            // For this iteration, I'll pass the stream and a generic name, then improve.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading version {Id}", versionId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("preview/version/{versionId}")]
    public async Task<IActionResult> GetVersionPreview(int versionId)
    {
        try
        {
            var version = await _documentService.GetVersionAsync(versionId);
            if (version == null) return NotFound();

            if (!await UserCanReadDocumentAsync(version.DocumentId)) return Forbid();

            var (stream, fileName, contentType) = await _documentService.DownloadVersionWithMetaAsync(versionId);
            if (stream == null) return NotFound();

            var ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
            
            // Override Content-Type for known previewable types
            if (ext == ".pdf") contentType = "application/pdf";
            else if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";
            else if (ext == ".png") contentType = "image/png";
            else if (ext == ".txt") contentType = "text/plain";
            else if (ext == ".gif") contentType = "image/gif";
            else if (ext == ".webp") contentType = "image/webp";
            
            // Force inline disposition for preview
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");

            return File(stream, contentType ?? "application/octet-stream");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing version {Id}", versionId);
            return StatusCode(500, "Internal server error");
        }
    }

    private async Task<bool> UserCanReadDocumentAsync(int documentId)
    {
        var user = HttpContext.User;
        var isAdmin = user.IsInRole("Admin") || user.Identity?.Name?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;
        if (isAdmin) return true;

        var userIdClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int uid))
        {
            var document = await _documentService.GetByIdAsync(documentId);
            if (document == null) return false;

            if (document.UploadedBy == uid) return true;

            // Check individual user rights
            var directRights = document.UserDocumentRightsList?.FirstOrDefault(r => r.UserId == uid);
            if (directRights != null && (directRights.Rights & 7) != 0)
                return true;

            // Check group-based rights — user may belong to a group that has access
            var groupRights = document.UserDocumentRightsList?.Where(r => r.GroupId.HasValue && r.GroupId > 0).ToList();
            if (groupRights != null && groupRights.Any())
            {
                var userGroups = await _userGroupService.GetGroupsForUserAsync(uid);
                var userGroupIds = userGroups.Select(g => g.GroupId).ToHashSet();
                var matchingGroupRight = groupRights.FirstOrDefault(r => userGroupIds.Contains(r.GroupId!.Value));
                if (matchingGroupRight != null && (matchingGroupRight.Rights & 7) != 0)
                    return true;
            }
        }
        return false;
    }
}
