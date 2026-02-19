using System.ComponentModel.DataAnnotations;

namespace DocumentManagementSystem.Models;

/// <summary>
/// Storage provider configuration for multi-backend storage
/// </summary>
public class StorageProvider
{
    public Guid StorageProviderId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ProviderType { get; set; } = "Local"; // Local, AzureBlob, S3, OneDrive

    [MaxLength(1000)]
    public string? ConnectionString { get; set; }

    [MaxLength(500)]
    public string? ContainerName { get; set; }

    [MaxLength(500)]
    public string? BasePath { get; set; }

    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Storage limits
    public long? MaxFileSizeBytes { get; set; }
    public long? MaxStorageBytes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
}
