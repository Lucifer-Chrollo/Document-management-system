using System.IO.Compression;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DocumentManagementSystem.Helpers;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Intelligent lossless compression service with algorithm selection based on file type
/// </summary>
public class CompressionService : ICompressionService
{
    private readonly ILogger<CompressionService> _logger;
    private readonly CompressionOptions _options;

    // File extensions that should skip compression (already compressed)
    private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".gz", ".bz2", ".xz",
        ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".webm",
        ".mp3", ".aac", ".flac", ".ogg", ".wma",
        // Images removed to allow attempted compression
        ".exe", ".dll"
    };

    // Extensions that benefit from high compression
    private static readonly HashSet<string> HighCompressionExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".csv", ".json", ".xml", ".html", ".css", ".js",
        ".cs", ".java", ".py", ".sql", ".md", ".yaml", ".yml"
    };

    public CompressionService(ILogger<CompressionService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _options = new CompressionOptions
        {
            MinimumFileSizeBytes = configuration.GetValue("Compression:MinimumFileSizeKB", 1) * 1024
        };
    }

    public CompressionResult Compress(Stream inputStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var originalSize = inputStream.Length;

        // Check if compression should be skipped
        if (!ShouldCompress(extension, originalSize))
        {
            _logger.LogDebug("Skipping compression for {FileName} (extension: {Ext}, size: {Size})", 
                fileName, extension, originalSize);
            
            inputStream.Position = 0;
            var passThrough = new TempFileStream();
            inputStream.CopyTo(passThrough);
            passThrough.Position = 0;

            return new CompressionResult
            {
                CompressedStream = passThrough,
                OriginalSize = originalSize,
                CompressedSize = originalSize,
                CompressionRatio = 0,
                Algorithm = "None",
                WasCompressed = false
            };
        }

        var algorithm = SelectAlgorithm(extension);
        var compressedStream = new TempFileStream(); // DISK BASED
        inputStream.Position = 0;

        try
        {
            switch (algorithm)
            {
                case "Brotli":
                    CompressWithBrotli(inputStream, compressedStream);
                    break;
                case "Deflate":
                    CompressWithDeflate(inputStream, compressedStream);
                    break;
                case "GZip":
                    CompressWithGZip(inputStream, compressedStream);
                    break;
                default:
                    CompressWithBrotli(inputStream, compressedStream);
                    algorithm = "Brotli";
                    break;
            }

            var compressedSize = compressedStream.Length;
            var compressionRatio = originalSize > 0 
                ? Math.Round((1 - (decimal)compressedSize / originalSize) * 100, 2) 
                : 0;

            // Only use compressed version if it's actually smaller
            if (compressedSize >= originalSize)
            {
                _logger.LogDebug("Compression not beneficial for {FileName}, keeping original", fileName);
                
                // Close/Delete the useless compressed file
                compressedStream.Dispose();
                
                inputStream.Position = 0;
                var originalCopy = new TempFileStream();
                inputStream.CopyTo(originalCopy);
                originalCopy.Position = 0;

                return new CompressionResult
                {
                    CompressedStream = originalCopy,
                    OriginalSize = originalSize,
                    CompressedSize = originalSize,
                    CompressionRatio = 0,
                    Algorithm = "None",
                    WasCompressed = false
                };
            }

            compressedStream.Position = 0;
            _logger.LogInformation("Compressed {FileName}: {Original} -> {Compressed} ({Ratio}%)", 
                fileName, originalSize, compressedSize, compressionRatio);

            return new CompressionResult
            {
                CompressedStream = compressedStream,
                OriginalSize = originalSize,
                CompressedSize = compressedSize,
                CompressionRatio = compressionRatio,
                Algorithm = algorithm,
                WasCompressed = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compression failed for {FileName}, keeping original", fileName);
            
            // Clean up potentially corrupted stream
            compressedStream.Dispose();

            inputStream.Position = 0;
            var fallback = new TempFileStream();
            inputStream.CopyTo(fallback);
            fallback.Position = 0;

            return new CompressionResult
            {
                CompressedStream = fallback,
                OriginalSize = originalSize,
                CompressedSize = originalSize,
                CompressionRatio = 0,
                Algorithm = "None",
                WasCompressed = false
            };
        }
    }

    public Task<CompressionResult> CompressAsync(Stream inputStream, string fileName)
    {
        return Task.Run(() => Compress(inputStream, fileName));
    }

    public Stream Decompress(Stream compressedStream, string algorithm)
    {
        try
        {
            var decompressedStream = new TempFileStream(); // Decompression also goes to disk
            compressedStream.Position = 0;

            switch (algorithm)
            {
                case "Brotli":
                    DecompressWithBrotli(compressedStream, decompressedStream);
                    break;
                case "Deflate":
                    DecompressWithDeflate(compressedStream, decompressedStream);
                    break;
                case "GZip":
                    DecompressWithGZip(compressedStream, decompressedStream);
                    break;
                case "None":
                default:
                    compressedStream.CopyTo(decompressedStream);
                    break;
            }

            decompressedStream.Position = 0;
            return decompressedStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decompression failed with algorithm {Algorithm}", algorithm);
            // Fallback: return copy of original
            var fallback = new TempFileStream();
            compressedStream.Position = 0;
            compressedStream.CopyTo(fallback);
            fallback.Position = 0;
            return fallback;
        }
    }

    public Task<Stream> DecompressAsync(Stream compressedStream, string algorithm)
    {
        return Task.Run(() => Decompress(compressedStream, algorithm));
    }

    // Helper methods remain same, they take abstract Stream
    public string SelectAlgorithm(string fileExtension)
    {
        var ext = fileExtension.ToLowerInvariant();
        
        // Text files benefit most from Brotli (best compression)
        if (HighCompressionExtensions.Contains(ext))
            return "Brotli";

        // PDF and Office documents - try Brotli first for better ratios
        if (ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx")
            return "Brotli";

        // Default to Brotli for best compression (lossless)
        return "Brotli";
    }

    public bool ShouldCompress(string fileExtension, long fileSize)
    {
        // Skip small files
        if (fileSize < _options.MinimumFileSizeBytes)
            return false;

        // Skip already compressed formats
        if (SkipExtensions.Contains(fileExtension.ToLowerInvariant()))
            return false;

        return true;
    }

    private void CompressWithDeflate(Stream input, Stream output)
    {
        var deflater = new Deflater(Deflater.BEST_COMPRESSION);
        using var deflateStream = new DeflaterOutputStream(output, deflater) { IsStreamOwner = false };
        input.CopyTo(deflateStream);
        deflateStream.Finish();
    }
    
    // Kept for async compatibility internally or unused private method removal
    private async Task CompressWithDeflateAsync(Stream input, Stream output)
    {
        await Task.Run(() => CompressWithDeflate(input, output));
    }

    private void CompressWithGZip(Stream input, Stream output)
    {
        using var gzipStream = new GZipOutputStream(output) { IsStreamOwner = false };
        gzipStream.SetLevel(9); // Max compression
        input.CopyTo(gzipStream);
        gzipStream.Finish();
    }
    
    private async Task CompressWithGZipAsync(Stream input, Stream output) { await Task.Run(() => CompressWithGZip(input, output)); }

    private void CompressWithBrotli(Stream input, Stream output)
    {
        // Use maximum compression level (11) for best lossless compression
        using var brotliStream = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true);
        input.CopyTo(brotliStream);
    }
    
    private async Task CompressWithBrotliAsync(Stream input, Stream output) { await Task.Run(() => CompressWithBrotli(input, output)); }

    private void DecompressWithBrotli(Stream input, Stream output)
    {
        using var brotliStream = new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true);
        brotliStream.CopyTo(output);
    }
    
    private async Task DecompressWithBrotliAsync(Stream input, Stream output) { await Task.Run(() => DecompressWithBrotli(input, output)); }

    private void DecompressWithDeflate(Stream input, Stream output)
    {
        using var inflateStream = new InflaterInputStream(input) { IsStreamOwner = false };
        inflateStream.CopyTo(output);
    }
    
    private async Task DecompressWithDeflateAsync(Stream input, Stream output) { await Task.Run(() => DecompressWithDeflate(input, output)); }

    private void DecompressWithGZip(Stream input, Stream output)
    {
        using var gzipStream = new GZipInputStream(input) { IsStreamOwner = false };
        gzipStream.CopyTo(output);
    }
    
    private async Task DecompressWithGZipAsync(Stream input, Stream output) { await Task.Run(() => DecompressWithGZip(input, output)); }
}
