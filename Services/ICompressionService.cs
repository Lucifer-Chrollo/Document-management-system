namespace DocumentManagementSystem.Services;

/// <summary>
/// Interface for lossless file compression operations
/// </summary>
public interface ICompressionService
{
    CompressionResult Compress(Stream inputStream, string fileName);
    Task<CompressionResult> CompressAsync(Stream inputStream, string fileName);
    Stream Decompress(Stream compressedStream, string algorithm);
    Task<Stream> DecompressAsync(Stream compressedStream, string algorithm);
    string SelectAlgorithm(string fileExtension);
    bool ShouldCompress(string fileExtension, long fileSize);
}

public class CompressionResult
{
    public Stream CompressedStream { get; set; } = Stream.Null;
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public decimal CompressionRatio { get; set; }
    public string Algorithm { get; set; } = string.Empty;
    public bool WasCompressed { get; set; }
}

public class CompressionOptions
{
    public int CompressionLevel { get; set; } = 9; // Max compression
    public long MinimumFileSizeBytes { get; set; } = 1024; // 1 KB minimum
    public string[] SkipExtensions { get; set; } = { ".zip", ".rar", ".7z", ".mp4", ".avi", ".jpg", ".jpeg", ".png", ".gif" };
}
