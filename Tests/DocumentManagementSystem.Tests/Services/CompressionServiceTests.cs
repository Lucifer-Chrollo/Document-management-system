using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using DocumentManagementSystem.Services.Compression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;
using Xunit;

namespace DocumentManagementSystem.Tests.Services;

public class CompressionServiceTests
{
    private readonly ICompressionService _service;
    private readonly IEnumerable<ICompressionStrategy> _strategies;

    public CompressionServiceTests()
    {
        // Setup Dependencies
        var logger = NullLogger<CompressionService>.Instance;
        
        var options = new CompressionOptions
        {
            Enabled = true,
            MinimumFileSizeKB = 0
        };

        _strategies = new List<ICompressionStrategy>
        {
            new BrotliStrategy(),
            new GZipStrategy(),
            new DeflateStrategy()
        };

        _service = new CompressionService(logger, Options.Create(options), _strategies);
    }

    [Fact]
    public void Compress_Should_Select_Brotli_For_Txt_Files()
    {
        // Arrange
        var input = new MemoryStream(Encoding.UTF8.GetBytes("Big text file content... This needs to be long enough to actually compress efficiently with Brotli overhead! " +
            "Adding more repetitive text to ensure the compressed version is smaller than the original string. " +
            "Brotli often has overhead for tiny strings, so we need a few hundred bytes here. " +
            "Repetition: Lorem ipsum dolor sit amet, consectetur adipiscing elit. Repetition: Lorem ipsum dolor sit amet."));
        
        // Act
        var result = _service.Compress(input, "file.txt");

        // Assert
        Assert.Equal("Brotli", result.Algorithm);
        Assert.NotNull(result.CompressedStream);
    }

    [Fact]
    public void Compress_Should_Select_PassThrough_For_Small_Files()
    {
        // Arrange (Below MinSize of 10 bytes)
        var input = new MemoryStream(Encoding.UTF8.GetBytes("Tiny"));

        // Act
        var result = _service.Compress(input, "file.txt");

        // Assert
        Assert.Equal("None", result.Algorithm);
        Assert.Equal(input.Length, result.CompressedSize);
    }
}
