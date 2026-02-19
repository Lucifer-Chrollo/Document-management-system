using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentManagementSystem.Services;

/// <summary>
/// AES-256 encryption service for protecting documents at rest
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly ILogger<EncryptionService> _logger;
    private readonly byte[] _key;
    private readonly bool _isEnabled;
    
    // AES-256 constants
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private const int IvSize = 16; // 128 bits for AES
    
    public bool IsEnabled => _isEnabled;

    public EncryptionService(ILogger<EncryptionService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        // Get encryption settings from configuration
        _isEnabled = configuration.GetValue("Encryption:Enabled", true);
        
        var keyString = configuration.GetValue<string>("Encryption:Key");
        
        if (string.IsNullOrEmpty(keyString))
        {
            // Generate a default key if not configured (NOT RECOMMENDED FOR PRODUCTION)
            // In production, this should come from Azure Key Vault, AWS KMS, or similar
            _logger.LogWarning("No encryption key configured! Using default key. Configure 'Encryption:Key' in appsettings.json for production.");
            keyString = "DMS_Default_Key_32BytesLong!!!!"; // 32 bytes = 256 bits
        }
        
        // Ensure key is exactly 32 bytes for AES-256
        _key = DeriveKey(keyString);
        
        _logger.LogInformation("Encryption service initialized. Enabled: {Enabled}", _isEnabled);
    }

    /// <summary>
    /// Encrypt a stream using AES-256-CBC with random IV (Synchronous)
    /// Writes directly to the output stream.
    /// </summary>
    public void Encrypt(Stream inputStream, Stream outputStream)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Encryption disabled, copying stream directly");
            inputStream.CopyTo(outputStream);
            return;
        }

        try
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Key = _key;
            aes.GenerateIV(); // Random IV for each encryption
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Write IV to the beginning of the output (needed for decryption)
            outputStream.Write(aes.IV, 0, aes.IV.Length);

            // Encrypt the data
            using var encryptor = aes.CreateEncryptor();
            // closeOutput: true - we want to keep the underlying file stream open if needed, 
            // BUT usually with CryptoStream for writing, closing it flushes the final block.
            // If we leave it open, we must manually FlushFinalBlock. 
            // However, CryptoStream does NOT support LeaveOpen in all constructors in older .NET, 
            // but in .NET 6+ it effectively does via the stream ownership.
            // Be careful: CryptoStream.Dispose() flushes final block. 
            // We want to write to outputStream but NOT close outputStream.
            
            using var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write, leaveOpen: true);
            inputStream.CopyTo(cryptoStream);
            cryptoStream.FlushFinalBlock();
            
            // Do NOT close outputStream here.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption failed");
            throw;
        }
    }

    public Task EncryptAsync(Stream inputStream, Stream outputStream)
    {
        return Task.Run(() => Encrypt(inputStream, outputStream));
    }

    /// <summary>
    /// Decrypt a stream that was encrypted with AES-256-CBC (Synchronous)
    /// Returns a readable stream (CryptoStream) that decrypts on the fly.
    /// </summary>
    public Stream Decrypt(Stream encryptedStream)
    {
        if (!_isEnabled)
        {
            return encryptedStream; // Return original stream
        }

        try
        {
            // Read the IV from the beginning of the stream
            var iv = new byte[IvSize];
            var bytesRead = encryptedStream.Read(iv, 0, IvSize);
            if (bytesRead != IvSize)
            {
                throw new CryptographicException("Invalid encrypted file: missing IV");
            }

            var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Key = _key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Create decryptor
            var decryptor = aes.CreateDecryptor();
            
            // Return a CryptoStream that reads from the underlying stream. 
            // DOES NOT buffer everything.
            // Note: We cannot wrap this in a 'using' block because we are returning it.
            // The caller is responsible for disposing the CryptoStream (which disposes the AES object? No, AES is separate).
            // Actually, we need to ensure AES is disposed. 
            // One way is to create a wrapper stream or rely on GC/Finalizers (bad).
            // Better: Let the CryptoStream take ownership? 
            // Standard practice: Return CryptoStream, but AES object might leak if not disposed?
            // In modern .NET, Aes implementation is often a wrapper around native.
            // FIX: We can't easily dispose AES if we return the stream derived from it. 
            // However, the decryptor implementation is what matters.
            
            return new CryptoStream(encryptedStream, decryptor, CryptoStreamMode.Read, leaveOpen: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption initialization failed");
            throw;
        }
    }

    public Task<Stream> DecryptAsync(Stream encryptedStream)
    {
        return Task.Run(() => Decrypt(encryptedStream));
    }

    /// <summary>
    /// Compute SHA-256 hash of a string (for blind indexing)
    /// </summary>
    public string Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Encrypt a string and return Base64 (Synchronous)
    /// </summary>
    public string EncryptText(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var tempStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(plainText));
        using var outputStream = new MemoryStream();
        
        Encrypt(tempStream, outputStream);
        
        return Convert.ToBase64String(outputStream.ToArray());
    }

    /// <summary>
    /// Encrypt a string and return Base64
    /// </summary>
    public Task<string> EncryptTextAsync(string plainText)
    {
        return Task.Run(() => EncryptText(plainText));
    }

    /// <summary>
    /// Decrypt a Base64 string (Synchronous)
    /// </summary>
    public string DecryptText(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        try 
        {
            var bytes = Convert.FromBase64String(cipherText);
            using var memoryStream = new MemoryStream(bytes);
            using var decryptedStream = Decrypt(memoryStream);
            using var reader = new StreamReader(decryptedStream);
            return reader.ReadToEnd();
        }
        catch (FormatException)
        {
            // Not Base64, assume plain text (migration scenario where legacy data exists)
             _logger.LogWarning("Failed to decrypt text (not base64), returning as-is.");
             return cipherText;
        }
        catch (CryptographicException)
        {
             _logger.LogError("Failed to decrypt text (key mismatch or corrupt), returning empty.");
             return "OFFLINE_OR_CORRUPT";
        }
    }

    /// <summary>
    /// Decrypt a Base64 string
    /// </summary>
    public Task<string> DecryptTextAsync(string cipherText)
    {
        return Task.Run(() => DecryptText(cipherText));
    }

    /// <summary>
    /// Derive a 256-bit key from any string using SHA-256
    /// </summary>
    private byte[] DeriveKey(string password)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
    }
}
