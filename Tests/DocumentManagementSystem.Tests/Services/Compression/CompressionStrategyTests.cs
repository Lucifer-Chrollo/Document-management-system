using System.IO.Compression;
using System.Text;
using DocumentManagementSystem.Services.Compression;
using Xunit;

namespace DocumentManagementSystem.Tests.Services.Compression;

public class CompressionStrategyTests
{
    [Fact]
    public void BrotliStrategy_Should_Compress_And_Decompress()
    {
        // Arrange
        var strategy = new BrotliStrategy();
        var originalText = "Hello World! This is a test string for Brotli compression.";
        var inputBytes = Encoding.UTF8.GetBytes(originalText);
        
        using var originalStream = new MemoryStream(inputBytes);
        using var compressedStream = new MemoryStream();
        using var decompressedStream = new MemoryStream();

        // Act - Compress
        strategy.Compress(originalStream, compressedStream);
        compressedStream.Position = 0;

        // Act - Decompress
        strategy.Decompress(compressedStream, decompressedStream);

        // Assert
        var decompressedText = Encoding.UTF8.GetString(decompressedStream.ToArray());
        Assert.Equal(originalText, decompressedText);
        Assert.True(compressedStream.Length > 0, "Compressed stream should not be empty");
    }

    [Fact]
    public void GZipStrategy_Should_Compress_And_Decompress()
    {
        // Arrange
        var strategy = new GZipStrategy();
        var originalText = "Hello World! This is a test string for GZip compression.";
        var inputBytes = Encoding.UTF8.GetBytes(originalText);

        using var originalStream = new MemoryStream(inputBytes);
        using var compressedStream = new MemoryStream();
        using var decompressedStream = new MemoryStream();

        // Act
        strategy.Compress(originalStream, compressedStream);
        compressedStream.Position = 0;
        strategy.Decompress(compressedStream, decompressedStream);

        // Assert
        var decompressedText = Encoding.UTF8.GetString(decompressedStream.ToArray());
        Assert.Equal(originalText, decompressedText);
        Assert.True(compressedStream.Length > 0);
    }

    [Fact]
    public void DeflateStrategy_Should_Compress_And_Decompress()
    {
        // Arrange
        var strategy = new DeflateStrategy();
        var originalText = "Hello World! This is a test string for Deflate compression.";
        var inputBytes = Encoding.UTF8.GetBytes(originalText);

        using var originalStream = new MemoryStream(inputBytes);
        using var compressedStream = new MemoryStream();
        using var decompressedStream = new MemoryStream();

        // Act
        strategy.Compress(originalStream, compressedStream);
        compressedStream.Position = 0;
        strategy.Decompress(compressedStream, decompressedStream);

        // Assert
        var decompressedText = Encoding.UTF8.GetString(decompressedStream.ToArray());
        Assert.Equal(originalText, decompressedText);
        Assert.True(compressedStream.Length > 0);
    }
}
