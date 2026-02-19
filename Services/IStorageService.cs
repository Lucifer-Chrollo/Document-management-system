namespace DocumentManagementSystem.Services;

/// <summary>
/// Interface for file storage operations (file system or cloud)
/// </summary>
public interface IStorageService
{
    string Save(Stream fileStream, string fileName, string? subfolder = null);
    Task<string> SaveAsync(Stream fileStream, string fileName, string? subfolder = null);
    // Saves a file using Content Addressable Storage (only writes if hash doesn't exist)
    string SaveCas(Stream fileStream, string hash, string originalExtension);
    Task<string> SaveCasAsync(Stream fileStream, string hash, string originalExtension);
    Stream? Get(string filePath);
    Task<Stream?> GetAsync(string filePath);
    bool Delete(string filePath);
    Task<bool> DeleteAsync(string filePath);
    bool Exists(string filePath);
    Task<bool> ExistsAsync(string filePath);
    long GetFileSize(string filePath);
    Task<long> GetFileSizeAsync(string filePath);
    string GetStoragePath();
}
