using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using Radzen.Blazor;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using DocumentManagementSystem.Helpers;
using System.Security.Claims;

namespace DocumentManagementSystem.Components.Pages;

/// <summary>
/// Main upload page.
/// Handles batch file selection, metadata entry (category, rights), and uploading with progress tracking.
/// </summary>
public partial class Upload
{
    [Inject] private IDocumentService DocumentService { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private ILocationService LocationService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private NotificationService NotificationService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IUserGroupService UserGroupService { get; set; } = default!;

    private RadzenDataGrid<UserDocumentRights> rightsGrid = default!;

    private Document documentMetadata = new Document { UploadedDate = DateTime.Now };
    private List<IBrowserFile> selectedFiles = new();
    private List<BatchItem> batchItems = new();
    private IEnumerable<Category> categories = Enumerable.Empty<Category>();
    private IEnumerable<Location> locations = Enumerable.Empty<Location>();

    private bool showPassword;
    private bool useGroups;
    private string userSearch = string.Empty;
    private bool canRead, canWrite, canDelete;

    private bool isUploading = false;
    private bool isDragOver = false;

    // ========================================================================================
    // PROGRESS TRACKING STATE
    // ========================================================================================
    // These variables allow the UI to show real-time feedback during the upload process.
    
    /// <summary>
    /// Tracks the overall percentage of the BATCH completion (e.g. 2 of 5 files = 40%).
    /// Displayed as the top progress bar.
    /// </summary>
    private double batchProgress = 0;
    
    /// <summary>
    /// User-facing text update (e.g. "Uploading file 1 of 5...").
    /// </summary>
    private string batchStatusText = string.Empty;
    
    /// <summary>
    /// Name of the specific file currently being sent to the server.
    /// </summary>
    private string currentFileName = string.Empty;
    
    /// <summary>
    /// Tracks the percentage completion of the SINGLE file currently being uploaded.
    /// </summary>
    private double fileProgress = 0;
    
    /// <summary>
    /// Number of bytes sent so far for the current file. Updated by the stream callback.
    /// </summary>
    private long bytesUploaded = 0;
    
    /// <summary>
    /// Total size of the current file being uploaded. Used to calculate 'fileProgress'.
    /// </summary>
    private long totalBytesForCurrentFile = 0;

    /// <summary>
    /// Initializes form data (Categories, Locations) and sets default permissions.
    /// Data Source: Fetched from 'DocumentService', which connects to the Database.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var isAdmin = user.Identity?.Name?.ToLower() == "admin" || user.IsInRole("Admin");

        try
        {
            // Load dropdown data from the database via Service layer
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (!isAdmin && idClaim != null && int.TryParse(idClaim.Value, out var userId))
            {
                categories = await CategoryService.GetCategoriesByUserAsync(userId);
            }
            else
            {
                categories = await CategoryService.GetCategoriesAsync();
            }

            locations = await LocationService.GetLocationsAsync();

            // ====================================================================================
            // DEFAULT PERMISSIONS (The "Rights" Logic)
            // ====================================================================================
            // 'Rights' is a bitmask integer representing binary flags:
            // 1 = Read
            // 2 = Write
            // 4 = Delete
            //
            // Rights = 7 means (1 + 2 + 4), i.e., Read + Write + Delete (Full Access).
            // "Power" is a default placeholder user created here to ensure someone always has full control.
            // ====================================================================================
            // No default permissions added initially.

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to load metadata: {ex.Message}");
        }
    }
    private void OnFilesSelected(InputFileChangeEventArgs e)
    {
        foreach (var file in e.GetMultipleFiles(10000))
        {
            if (!selectedFiles.Any(f => f.Name == file.Name && f.Size == file.Size))
            {
                selectedFiles.Add(file);
                batchItems.Add(new BatchItem
                {
                    File = file,
                    SaveAsName = string.IsNullOrEmpty(documentMetadata.DocumentName)
                        ? Path.GetFileNameWithoutExtension(file.Name)
                        : documentMetadata.DocumentName
                });
            }
        }
    }

    private void RemoveFile(IBrowserFile file)
    {
        selectedFiles.Remove(file);
        batchItems.RemoveAll(i => i.File == file);
    }

    public class BatchItem
    {
        public IBrowserFile File { get; set; } = default!;
        public string SaveAsName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Adds a user or expands a group into individual users for the document metadata.
    /// Constructs the 'Rights' integer by combining selected checkboxes.
    /// </summary>
    private async Task AddUserRight()
    {
        if (string.IsNullOrWhiteSpace(userSearch))
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Validation", "Please enter a user or group name.");
            return;
        }

        // Construct the rights list and calculate bitmask
        var rightsList = new List<string>();
        if (canRead) rightsList.Add("Read");
        if (canWrite) rightsList.Add("Write");
        if (canDelete) rightsList.Add("Delete");

        int calculatedRights = (canRead ? 1 : 0) | (canWrite ? 2 : 0) | (canDelete ? 4 : 0);

        if (calculatedRights == 0)
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Validation", "Please select at least one permission (Read, Write, or Delete).");
            return;
        }

        // Fetch all groups matching the search string (case-insensitive) to allow auto-detection
        Console.WriteLine($"[Upload.AddUserRight] User searched for: '{userSearch}'");
        
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var isAdmin = user.Identity?.Name?.ToLower() == "admin" || user.IsInRole("Admin");
        
        IEnumerable<UserGroup> allGroups;
        if (isAdmin)
        {
            allGroups = await UserGroupService.GetAllGroupsAsync();
        }
        else
        {
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim != null && int.TryParse(idClaim.Value, out var currentUserId))
            {
                var created = await UserGroupService.GetGroupsCreatedByUserAsync(currentUserId);
                var member = await UserGroupService.GetGroupsForUserAsync(currentUserId);
                allGroups = created.Union(member).DistinctBy(g => g.GroupId).ToList();
            }
            else
            {
                allGroups = Enumerable.Empty<UserGroup>();
            }
        }

        Console.WriteLine($"[Upload.AddUserRight] Found {allGroups.Count()} total groups in system.");
        
        var targetGroup = allGroups.FirstOrDefault(g => g.GroupName.Equals(userSearch, StringComparison.OrdinalIgnoreCase));
        if (targetGroup != null) 
        {
            Console.WriteLine($"[Upload.AddUserRight] Matched targetGroup: ID={targetGroup.GroupId}, Name='{targetGroup.GroupName}'");
        } 
        else 
        {
            Console.WriteLine($"[Upload.AddUserRight] No target group matched.");
        }

        if (useGroups || targetGroup != null)
        {
            try
            {
                if (targetGroup == null)
                {
                    Console.WriteLine("[Upload.AddUserRight] Error: Group not found (but useGroups was checked).");
                    NotificationService.Notify(NotificationSeverity.Error, "Not Found", $"Could not find a group named '{userSearch}'.");
                    return;
                }

                // Expand the group into individual users
                var members = await UserGroupService.GetGroupMembersDetailedAsync(targetGroup.GroupId);
                Console.WriteLine($"[Upload.AddUserRight] Fetched {members.Count()} detailed members for group {targetGroup.GroupId}.");
                
                if (!members.Any())
                {
                    Console.WriteLine("[Upload.AddUserRight] Warning: Group is empty.");
                    NotificationService.Notify(NotificationSeverity.Warning, "Empty Group", $"The group '{targetGroup.GroupName}' has no members.");
                    return;
                }

                int addedCount = 0;
                foreach (var member in members)
                {
                    Console.WriteLine($"[Upload.AddUserRight] Processing member: UserId={member.UserId}, UserName='{member.UserName}'");
                    // Prevent duplicate user entries
                    if (!documentMetadata.UserDocumentRightsList.Any(r => r.UserId == member.UserId))
                    {
                        documentMetadata.UserDocumentRightsList.Add(new UserDocumentRights
                        {
                            UserName = !string.IsNullOrWhiteSpace(member.UserName) ? member.UserName.Trim() : member.Email ?? $"User {member.UserId}",
                            RightsName = string.Join(", ", rightsList),
                            Rights = calculatedRights,
                            UserId = member.UserId,
                            GroupId = null 
                        });
                        addedCount++;
                        Console.WriteLine($"[Upload.AddUserRight] Added member to list: UserId={member.UserId}");
                    }
                    else 
                    {
                        Console.WriteLine($"[Upload.AddUserRight] Skipped duplicate member: UserId={member.UserId}");
                    }
                }
                
                Console.WriteLine($"[Upload.AddUserRight] Group expansion complete. Added {addedCount} members.");
                NotificationService.Notify(NotificationSeverity.Success, "Group Expanded", $"Added {addedCount} members from group '{targetGroup.GroupName}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Upload.AddUserRight] EXCEPTION: {ex}");
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to expand group: {ex.Message}");
                return;
            }
        }
        else
        {
            // Legacy single user behavior
            documentMetadata.UserDocumentRightsList.Add(new UserDocumentRights
            {
                UserName = userSearch,
                RightsName = string.Join(", ", rightsList),
                Rights = calculatedRights,
                UserId = 0, // Placeholder if no explicit lookup is done
                GroupId = null
            });
            NotificationService.Notify(NotificationSeverity.Info, "Success", $"Added sharing rights for {documentMetadata.UserDocumentRightsList.Last().UserName}");
        }

        // Reset inputs
        userSearch = string.Empty;
        canRead = canWrite = canDelete = false;
        
        // Force UI update
        StateHasChanged();
        if (rightsGrid != null)
        {
            await rightsGrid.Reload();
        }
    }

    private async Task RemoveUserRight(UserDocumentRights right)
    {
        documentMetadata.UserDocumentRightsList.Remove(right);
        if (rightsGrid != null)
        {
            await rightsGrid.Reload();
        }
    }

    /// <summary>
    /// Processes the upload batch.
    /// Iterates through selected files, creates a copy of metadata for each, and uploads sequentially.
    /// Uses 'DocumentService' to persist data to Database/Storage.
    /// </summary>
    private async Task OnSubmit()
    {
        if (!selectedFiles.Any())
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Please select at least one file.");
            return;
        }

        isUploading = true;
        batchProgress = 0;
        int uploadedCount = 0;

        try
        {
            // Get Current User ID from Auth Provider (Identity)
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            foreach (var item in batchItems.ToList())
            {
                // Prepare UI for current file upload
                var file = item.File;
                currentFileName = item.SaveAsName;
                batchStatusText = $"Uploading file {uploadedCount + 1} of {batchItems.Count}...";
                totalBytesForCurrentFile = file.Size;
                
                // Reset file-level progress
                bytesUploaded = 0;
                fileProgress = 0;
                StateHasChanged(); // Refresh UI

                try
                {
                    // Clone the shared metadata (Category, Rights, etc.) for this specific file
                    var doc = documentMetadata.Clone();
                    doc.DocumentName = item.SaveAsName;
                    doc.SourcePath = file.Name;
                    doc.Extension = Path.GetExtension(file.Name);
                    doc.FileSize = file.Size;
                    doc.FileType = file.ContentType;
                    doc.ParentID = 0; 

                    // Create stream for reading file
                    using var baseStream = file.OpenReadStream(500 * 1024 * 1024); // Limit to 500MB
                    
                    // Wrap the stream in a helper that reports bytes read -> updates 'bytesUploaded'
                    using var progressStream = new ProgressStream(baseStream, (bytes) => {
                        bytesUploaded = bytes;
                        // Calculate percentage: (Current Bytes / Total Bytes) * 100
                        fileProgress = (double)bytes / totalBytesForCurrentFile * 100;
                        InvokeAsync(StateHasChanged); // Force UI update on every chunk
                    });

                    // SERVICE CALL: Send document info & stream to the backend
                    var response = await DocumentService.CreateAsync(doc, progressStream, userId);
                    if (!response.Result) throw new Exception(response.Message);

                    uploadedCount++;
                    // Calculate overall batch progress
                    batchProgress = (double)uploadedCount / selectedFiles.Count * 100;
                    
                    // Remove from list so user sees it disappear as done
                    selectedFiles.Remove(file);
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to upload {file.Name}: {ex.Message}");
                }
            }

            NotificationService.Notify(NotificationSeverity.Success, "Upload Complete", $"{uploadedCount} documents processed successfully.");

            if (!selectedFiles.Any())
            {
                // Redirect to Browse page on full success
                NavigationManager.NavigateTo("/ManageDocuments");
            }
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Batch Error", ex.Message);
        }
        finally
        {
            isUploading = false;
            StateHasChanged();
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

    [Inject] private DialogService DialogService { get; set; } = default!;

    private async Task CreateCategoryDialog()
    {
        var result = await DialogService.OpenAsync<CreateCategoryDialog>("Create New Category", 
            new Dictionary<string, object>(),
            new DialogOptions() { Width = "400px", Height = "200px" });

        // The dialog returns the string name if created, or null if cancelled
        string? newCategoryName = result as string;

        if (!string.IsNullOrWhiteSpace(newCategoryName))
        {
            try
            {
                var newCategory = new Category 
                { 
                    CategoryName = newCategoryName,
                    Description = "Created via Upload Page",
                    CategoryOrder = 0
                };
                
                var response = await CategoryService.CreateCategoryAsync(newCategory);
                
                if (response.Result)
                {
                    NotificationService.Notify(NotificationSeverity.Success, "Success", $"Category '{newCategoryName}' created.");
                    // Refresh the list so the new category appears in the dropdown
                    categories = await CategoryService.GetCategoriesAsync();
                    // Select the newly created category
                    documentMetadata.CategoryID = (int)response.RecordID;
                    StateHasChanged();
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Error", response.Message);
                }
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", $"Failed to create category: {ex.Message}");
            }
        }
    }
}
