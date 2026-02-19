using System;
using System.IO;

namespace DocumentManagementSystem.Helpers;

/// <summary>
/// A FileStream that automatically deletes the underlying file when disposed.
/// Useful for temporary large file processing to avoid memory pressure.
/// </summary>
public class TempFileStream : FileStream
{
    private readonly string _filePath;

    public TempFileStream() : this(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tmp"))
    {
    }

    public TempFileStream(string path) 
        : base(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose)
    {
        _filePath = path;
    }

    // FileOptions.DeleteOnClose handles the deletion automatically when the handle is closed!
    // But explicitly storing path just in case we need it or debugging.
}
