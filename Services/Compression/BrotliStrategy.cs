using System.IO.Compression;

namespace DocumentManagementSystem.Services.Compression;

/// <summary>
/// Brotli compression strategy — best for text-heavy and document files.
/// Uses maximum compression level (11) for smallest output size.
/// </summary>
public class BrotliStrategy : ICompressionStrategy
{
    public string Name => "Brotli";

    public void Compress(Stream input, Stream output)
    {
        using var brotliStream = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true);
        input.CopyTo(brotliStream);
    }

    public void Decompress(Stream input, Stream output)
    {
        using var brotliStream = new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true);
        brotliStream.CopyTo(output);
    }
}
