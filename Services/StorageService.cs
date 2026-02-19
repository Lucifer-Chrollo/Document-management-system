using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentManagementSystem.Services;
/// <summary>
/// File system storage service with AES-256 encryption for local development
/// </summary>
public class StorageService : IStorageService
{
    private readonly string _storagePath;
    private readonly ILogger<StorageService> _logger;
    private readonly IEncryptionService _encryptionService;

    public StorageService(IConfiguration configuration, ILogger<StorageService> logger, IEncryptionService encryptionService)
    {
        _storagePath = configuration["Storage:LocalPath"] 
            ?? Path.Combine(Directory.GetCurrentDirectory(), "DocumentStorage");
        _logger = logger;
        _encryptionService = encryptionService;

        // Ensure storage directory exists
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
            _logger.LogInformation("Created storage directory: {Path}", _storagePath);
        }
        
        _logger.LogInformation("StorageService initialized. Encryption enabled: {Enabled}", _encryptionService.IsEnabled);
    }

    public string Save(Stream fileStream, string fileName, string? subfolder = null)
    {
        try
        {
            // Generate unique path with date-based organization
            var datePath = DateTime.UtcNow.ToString("yyyy/MM/dd");
            var uniqueId = Guid.NewGuid().ToString("N")[..8];
            var safeFileName = SanitizeFileName(fileName);
            
            // Add .enc extension if encryption is enabled
            var storedFileName = _encryptionService.IsEnabled 
                ? $"{uniqueId}_{safeFileName}.enc" 
                : $"{uniqueId}_{safeFileName}";
            
            var relativePath = string.IsNullOrEmpty(subfolder)
                ? Path.Combine(datePath, storedFileName)
                : Path.Combine(subfolder, datePath, storedFileName);

            var fullPath = Path.Combine(_storagePath, relativePath);
            var directory = Path.GetDirectoryName(fullPath)!;

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Stream directly to disk
            using var fileStreamOut = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            fileStream.Position = 0;
            
            if (_encryptionService.IsEnabled)
            {
                _encryptionService.Encrypt(fileStream, fileStreamOut); // Streams directly
            }
            else
            {
                fileStream.CopyTo(fileStreamOut);
            }

            _logger.LogInformation("Saved {Encrypted} file to: {Path}", 
                _encryptionService.IsEnabled ? "encrypted" : "unencrypted", relativePath);
            return relativePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file {FileName}", fileName);
            throw;
        }
    }

    public Task<string> SaveAsync(Stream fileStream, string fileName, string? subfolder = null)
    {
        return Task.Run(() => Save(fileStream, fileName, subfolder));
    }

    public string SaveCas(Stream fileStream, string hash, string originalExtension)
    {
        try
        {
            // Structure: CAS/{shard}/{hash}{ext}
            // Shard: First 2 chars of hash (00-ff)
            if (string.IsNullOrEmpty(hash) || hash.Length < 2) throw new ArgumentException("Invalid hash");

            var shard = hash.Substring(0, 2);
            var fileName = hash; // Filename is the hash
            
            // We append .enc if encryption is enabled, but NOT the original extension.
            
            if (_encryptionService.IsEnabled) 
                fileName += ".enc";

            var relativePath = Path.Combine("CAS", shard, fileName);
            var fullPath = Path.Combine(_storagePath, relativePath);

            // DEDUPLICATION CHECK
            if (File.Exists(fullPath))
            {
                _logger.LogInformation("CAS Deduplication hit! File {Hash} already exists. Skipping write.", hash);
                return relativePath;
            }

            // File doesn't exist, create it
            var directory = Path.GetDirectoryName(fullPath)!;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Stream directly to disk
            using var fileStreamOut = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            fileStream.Position = 0;
            
            if (_encryptionService.IsEnabled)
            {
                _encryptionService.Encrypt(fileStream, fileStreamOut);
            }
            else
            {
                fileStream.CopyTo(fileStreamOut);
            }

            _logger.LogInformation("Saved new CAS file: {Path}", relativePath);
            return relativePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving CAS file {Hash}", hash);
            throw;
        }
    }

    public Task<string> SaveCasAsync(Stream fileStream, string hash, string originalExtension)
    {
        return Task.Run(() => SaveCas(fileStream, hash, originalExtension));
    }

    public Stream? Get(string filePath)
    {
        try
        {
            var fullPath = Path.Combine(_storagePath, filePath);
            
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("File not found: {Path}", filePath);
                return null;
            }

            // Return FileStream directly (caller must dispose)
            // Use FileShare.Read to allow concurrent reads
            var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            
            // Decrypt if file has .enc extension (was encrypted)
            if (filePath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Decrypting file stream: {Path}", filePath);
                // Return CryptoStream wrapping FileStream
                return _encryptionService.Decrypt(fileStream);
            }
            
            return fileStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file {FilePath}", filePath);
            return null;
        }
    }

    public Task<Stream?> GetAsync(string filePath)
    {
        return Task.Run(() => Get(filePath));
    }

    public bool Delete(string filePath)
    {
        try
        {
            var fullPath = Path.Combine(_storagePath, filePath);
            
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted file: {Path}", filePath);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FilePath}", filePath);
            return false;
        }
    }

    public Task<bool> DeleteAsync(string filePath)
    {
        return Task.Run(() => Delete(filePath));
    }

    public bool Exists(string filePath)
    {
        var fullPath = Path.Combine(_storagePath, filePath);
        return File.Exists(fullPath);
    }

    public Task<bool> ExistsAsync(string filePath)
    {
        return Task.Run(() => Exists(filePath));
    }

    public long GetFileSize(string filePath)
    {
        var fullPath = Path.Combine(_storagePath, filePath);
        
        if (File.Exists(fullPath))
        {
            var fileInfo = new FileInfo(fullPath);
            return fileInfo.Length;
        }
        
        return 0L;
    }

    public Task<long> GetFileSizeAsync(string filePath)
    {
        return Task.Run(() => GetFileSize(filePath));
    }

    public string GetStoragePath() => _storagePath;

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }
}
