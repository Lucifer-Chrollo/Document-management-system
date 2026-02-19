using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace DocumentManagementSystem.Services.Compression;

/// <summary>
/// Deflate compression strategy — raw Deflate using SharpZipLib.
/// Uses best compression level for maximum size reduction.
/// </summary>
public class DeflateStrategy : ICompressionStrategy
{
    public string Name => "Deflate";

    public void Compress(Stream input, Stream output)
    {
        var deflater = new Deflater(Deflater.BEST_COMPRESSION);
        using var deflateStream = new DeflaterOutputStream(output, deflater) { IsStreamOwner = false };
        input.CopyTo(deflateStream);
        deflateStream.Finish();
    }

    public void Decompress(Stream input, Stream output)
    {
        using var inflateStream = new InflaterInputStream(input) { IsStreamOwner = false };
        inflateStream.CopyTo(output);
    }
}
