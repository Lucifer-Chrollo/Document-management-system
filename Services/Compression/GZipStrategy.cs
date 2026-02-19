using ICSharpCode.SharpZipLib.GZip;

namespace DocumentManagementSystem.Services.Compression;

/// <summary>
/// GZip compression strategy — widely compatible, good for general-purpose use.
/// Uses SharpZipLib for maximum compression level (9).
/// </summary>
public class GZipStrategy : ICompressionStrategy
{
    public string Name => "GZip";

    public void Compress(Stream input, Stream output)
    {
        using var gzipStream = new GZipOutputStream(output) { IsStreamOwner = false };
        gzipStream.SetLevel(9); // Max compression
        input.CopyTo(gzipStream);
        gzipStream.Finish();
    }

    public void Decompress(Stream input, Stream output)
    {
        using var gzipStream = new GZipInputStream(input) { IsStreamOwner = false };
        gzipStream.CopyTo(output);
    }
}
