using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Radzen;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using DocumentManagementSystem.Data.Repositories;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;

namespace DocumentManagementSystem.Components.Pages
{
    public partial class DocumentView
    {
        [Parameter] public int Id { get; set; }
        private Document? document;
        private IEnumerable<DocumentVersion> history = Enumerable.Empty<DocumentVersion>();

        // UI State for Comments and Download
        private bool isDownloading = false;
        private double downloadProgress = 0;
        private long bytesDownloaded = 0;
        private long totalBytesToDownload = 0;
        private DotNetObjectReference<DocumentView>? dotNetHelper;

        // Comment State
        private string newCommentText = string.Empty;
        private bool isPostingComment = false;

        // Password Logic
        private bool isUnlocked = false;
        private string enteredPassword = string.Empty;
        private bool passwordError = false;
        private bool showDocPassword = false;

        // Permissions State
        private bool canRead = false;
        private bool canWrite = false;
        private bool canDelete = false;
        private bool isAdmin = false;
        private int? currentUserId = null;

        protected override async Task OnParametersSetAsync()
        {
            try
            {
                document = await DocumentService.GetByIdAsync(Id);
                if (document == null)
                {
                    NotificationService.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Error,
                        Summary = "Error",
                        Detail = "Document not found or error loading data."
                    });
                }
                else
                {
                    await EvaluatePermissions();
                    // Auto-unlock if no password OR if current user is Admin
                    isUnlocked = string.IsNullOrEmpty(document.Password) || isAdmin;
                    await LoadHistory();
                }
            }
            catch (Exception)
            {
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = "Error",
                    Detail = "An unexpected error occurred while loading the document."
                });
            }
        }

        private async Task EvaluatePermissions()
        {
            canRead = false;
            canWrite = false;
            canDelete = false;

            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                // Check admin status first — before any claim parsing
                isAdmin = user.IsInRole("Admin") || user.Identity.Name?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;

                if (isAdmin)
                {
                    canRead = true;
                    canWrite = true;
                    canDelete = true;
                    // Try to parse userId but don't block on it
                    var adminIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(adminIdClaim, out int adminUid))
                        currentUserId = adminUid;
                    return;
                }

                var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int uid))
                {
                    currentUserId = uid;

                    if (document?.UploadedBy == currentUserId)
                    {
                        canRead = true;
                        canWrite = true;
                        canDelete = true;
                    }
                    else if (document?.UserDocumentRightsList != null)
                    {
                        // Otherwise, check explicit shares
                        var userRights = document.UserDocumentRightsList.FirstOrDefault(r => r.UserId == currentUserId);
                        if (userRights != null)
                        {
                            // Permission hierarchy: Delete(4) > Write(2) > Read(1)
                            // Delete implies all permissions, Write implies Read
                            bool hasRead = (userRights.Rights & 1) == 1;
                            bool hasWrite = (userRights.Rights & 2) == 2;
                            bool hasDelete = (userRights.Rights & 4) == 4;

                            canDelete = hasDelete;
                            canWrite = hasWrite || hasDelete;   // Delete implies write
                            canRead = hasRead || hasWrite || hasDelete; // Write/Delete imply read
                        }
                    }
                }
            }

            if (!canRead)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Access Denied", "You do not have permission to view this document.");
                NavigationManager.NavigateTo("/ManageDocuments");
            }
        }

        private async Task PostComment()
        {
            if (string.IsNullOrWhiteSpace(newCommentText) || document == null || !currentUserId.HasValue) return;

            isPostingComment = true;
            try
            {
                var comment = new Comments
                {
                    CommentDate = DateTime.UtcNow,
                    Comment = newCommentText,
                    DocumentID = document.DocumentId,
                    CommentBy = currentUserId.Value
                };

                var response = await CommentService.AddAsync(comment);

                if (response.Result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success", "Comment added.");
                    newCommentText = string.Empty;

                    // Refresh document to get the new comment list with the resolved names from the database
                    document = await DocumentService.GetByIdAsync(Id);
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error", response.Message ?? "Failed to add comment.");
                }
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to add comment: {ex.Message}");
            }
            finally
            {
                isPostingComment = false;
            }
        }

        private void GoBack()
        {
            if (document?.CategoryID > 0)
                NavigationManager.NavigateTo($"/browse/{document.CategoryID}");
            else
                NavigationManager.NavigateTo("/ManageDocuments");
        }

        private async Task DownloadDocument()
        {
            if (document == null) return;

            isDownloading = true;
            downloadProgress = 0;
            bytesDownloaded = 0;
            totalBytesToDownload = document.FileSize;
            StateHasChanged();

            try
            {
                dotNetHelper = DotNetObjectReference.Create(this);
                var url = $"/api/files/download/{Id}";
                if (!string.IsNullOrEmpty(document.Password))
                {
                    url += $"?pwd={Uri.EscapeDataString(document.Password)}";
                }
                var fileName = document.DocumentName + (document.Extension ?? "");

                await JSRuntime.InvokeVoidAsync("downloadWithProgress", url, fileName, dotNetHelper);
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Download failed: {ex.Message}");
            }
            finally
            {
                isDownloading = false;
                StateHasChanged();
            }
        }

        [JSInvokable]
        public void OnDownloadProgress(long receivedLength, long totalLength)
        {
            bytesDownloaded = receivedLength;
            if (totalLength > 0)
            {
                totalBytesToDownload = totalLength;
                downloadProgress = (double)receivedLength / totalLength * 100;
            }
            else if (totalBytesToDownload > 0)
            {
                downloadProgress = (double)receivedLength / totalBytesToDownload * 100;
            }

            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            dotNetHelper?.Dispose();
        }

        private async Task ShowEditDialog()
        {
            // TODO: Implement edit dialog
        }

        private async Task UploadNewVersion()
        {
            try
            {
                Console.WriteLine("[DEBUG] Opening UploadVersionDialog for DocId: {document.DocumentId}");
                var result = await DialogService.OpenAsync<UploadVersionDialog>("New Version: " + document?.DocumentName,
                       new Dictionary<string, object> { { "DocumentId", Id }, { "DocumentName", document?.DocumentName ?? "" } },
                       new DialogOptions() { Width = "500px", Height = "auto", Resizable = false, Draggable = true });

                Console.WriteLine("[DEBUG] UploadVersionDialog closed. Result: {result}");

                if (result == true)
                {
                    document = await DocumentService.GetByIdAsync(Id); // Reload doc
                    await LoadHistory();
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] UploadNewVersion failed: " + ex.GetType().Name + " - " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Could not open upload dialog: {ex.Message}");
            }
        }


        private async Task LoadHistory()
        {
            history = await DocumentService.GetHistoryAsync(Id);
        }

        private async Task DownloadVersion(DocumentVersion version)
        {
            try
            {
                // Use JS download for version
                var url = $"/api/files/download/version/{version.VersionId}";
                var fileName = version.FileName;
                await JSRuntime.InvokeVoidAsync("triggerFileDownload", url, fileName);
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Download failed.");
            }
        }

        private async Task PreviewVersion(DocumentVersion version)
        {
            try
            {
                // Open preview in new tab
                var url = $"/api/files/preview/version/{version.VersionId}";
                await JSRuntime.InvokeVoidAsync("open", url, "_blank");
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Preview failed.");
            }
        }

        private async Task RestoreVersion(DocumentVersion version)
        {
            var confirmed = await DialogService.Confirm($"Restore version {version.VersionNumber}? This will create a new version.", "Restore Version");
            if (confirmed == true)
            {
                var success = await DocumentService.RestoreVersionAsync(version.VersionId);
                if (success.Result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Restored", "Version restored successfully.");
                    document = await DocumentService.GetByIdAsync(Id);
                    await LoadHistory();
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to restore version.");
                }
            }
        }

        private async Task DeleteDocument()
        {
            var confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete '{document?.DocumentName}'?",
                "Delete Document",
                new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

            if (confirmed == true)
            {
                await DocumentService.DeleteAsync(Id);
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = "Deleted",
                    Detail = "Document moved to trash",
                    Duration = 3000
                });
                GoBack();
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
            return $"{size:0.##} {sizes[order]}";
        }

        private static string GetFileIcon(string? ext) => ext?.ToLowerInvariant() switch
        {
            ".pdf" => "picture_as_pdf", ".doc" or ".docx" => "description",
            ".xls" or ".xlsx" => "table_chart", ".jpg" or ".jpeg" or ".png" => "image",
            _ => "insert_drive_file"
        };

        private static string GetIconClass(string? ext) => ext?.ToLowerInvariant() switch
        {
            ".pdf" => "pdf", ".doc" or ".docx" => "word", ".xls" or ".xlsx" => "excel",
            ".jpg" or ".jpeg" or ".png" or ".webp" => "image", _ => "default"
        };

        private static bool IsImage(string? ext)
        {
            var e = ext?.ToLowerInvariant();
            return e == ".jpg" || e == ".jpeg" || e == ".png" || e == ".webp" || e == ".bmp" || e == ".gif";
        }

        private async Task OnShareDocument()
        {
            if (document == null) return;

            var result = await DialogService.OpenAsync<ShareDocumentDialog>("Share Document",
                new Dictionary<string, object> {
                    { "DocumentName", document.DocumentName },
                    { "UserId", currentUserId ?? 0 },
                    { "IsAdmin", isAdmin }
                },
                new DialogOptions { Width = "500px", Height = "auto", Resizable = false, Draggable = true });

            if (result != null)
            {
                try
                {
                    var shareData = (dynamic)result;
                    int groupId = (int)shareData.GroupId;
                    int rights = (int)shareData.Rights;
                    await DocumentRepository.GrantGroupAccessAsync(document.DocumentId, groupId, rights);

                    // Refresh document to show updated rights
                    document = await DocumentService.GetByIdAsync(Id);
                    StateHasChanged();

                    NotificationService.Notify(NotificationSeverity.Success, "Shared", "Document successfully shared with the group.");
                }
                catch (Exception ex)
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error", $"Could not share document: {ex.Message}");
                }
            }
        }

        private void UnlockDocument()
        {
            if (document?.Password == enteredPassword)
            {
                isUnlocked = true;
                passwordError = false;
            }
            else
            {
                passwordError = true;
            }
            StateHasChanged();
        }
    }
}


