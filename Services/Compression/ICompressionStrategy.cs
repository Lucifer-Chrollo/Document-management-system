namespace DocumentManagementSystem.Services.Compression;

/// <summary>
/// Strategy interface for compression algorithms.
/// Each implementation encapsulates a specific compression algorithm (Brotli, GZip, Deflate).
/// </summary>
/// <remarks>
/// <b>Design Pattern:</b> Strategy Pattern (GoF)
/// <para>
/// Instead of a <c>switch</c> statement selecting an algorithm, each algorithm is its own class.
/// This makes the system <b>Open for extension, Closed for modification</b> (Open/Closed Principle).
/// To add a new algorithm (e.g., Zstandard), just create a new class — no existing code changes.
/// </para>
/// </remarks>
public interface ICompressionStrategy
{
    /// <summary>
    /// The name of this algorithm (e.g., "Brotli", "GZip", "Deflate").
    /// Used as the key for lookup and stored in the database.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Compresses data from the input stream to the output stream.
    /// </summary>
    void Compress(Stream input, Stream output);

    /// <summary>
    /// Decompresses data from the input stream to the output stream.
    /// </summary>
    void Decompress(Stream input, Stream output);
}
