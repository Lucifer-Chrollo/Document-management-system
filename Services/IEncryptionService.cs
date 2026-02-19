namespace DocumentManagementSystem.Services;

/// <summary>
/// Service for AES-256 encryption/decryption of document content
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypt a stream using AES-256
    /// </summary>
    /// Encrypt a stream using AES-256 (Synchronous)
    /// </summary>


    /// <summary>
    /// Encrypt a stream using AES-256-CBC with random IV (Synchronous)
    /// Writes directly to the output stream.
    /// </summary>
    void Encrypt(Stream inputStream, Stream outputStream);

    /// <summary>
    /// Encrypt a stream using AES-256
    /// </summary>

    Task EncryptAsync(Stream inputStream, Stream outputStream);
    
    /// <summary>
    /// Decrypt a stream that was encrypted with AES-256-CBC (Synchronous)
    /// Returns a readable stream (CryptoStream) that decrypts on the fly.
    /// </summary>
    Stream Decrypt(Stream encryptedStream);

    /// <summary>
    /// Decrypt a stream that was encrypted with AES-256
    /// </summary>
    Task<Stream> DecryptAsync(Stream encryptedStream);
    
    /// <summary>
    /// Check if encryption is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Compute SHA-256 hash (Blind Indexing)
    /// </summary>
    string Hash(string input);

    /// <summary>
    /// Encrypt a string and return Base64 (Synchronous)
    /// </summary>
    string EncryptText(string plainText);

    /// <summary>
    /// Encrypt a string and return Base64
    /// </summary>
    Task<string> EncryptTextAsync(string plainText);

    /// <summary>
    /// Decrypt a Base64 string (Synchronous)
    /// </summary>
    string DecryptText(string cipherText);

    /// <summary>
    /// Decrypt a Base64 string
    /// </summary>
    Task<string> DecryptTextAsync(string cipherText);
}
