using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentManagementSystem.Helpers;

public class ProgressStream : Stream
{
    private readonly Stream _innerStream;
    private readonly Action<long> _onProgress;
    private long _totalBytesRead;

    public ProgressStream(Stream innerStream, Action<long> onProgress)
    {
        _innerStream = innerStream;
        _onProgress = onProgress;
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;
    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override void Flush() => _innerStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = _innerStream.Read(buffer, offset, count);
        ReportProgress(bytesRead);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        ReportProgress(bytesRead);
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int bytesRead = await _innerStream.ReadAsync(buffer, cancellationToken);
        ReportProgress(bytesRead);
        return bytesRead;
    }

    private void ReportProgress(int bytesRead)
    {
        if (bytesRead > 0)
        {
            _totalBytesRead += bytesRead;
            _onProgress?.Invoke(_totalBytesRead);
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
    public override void SetLength(long value) => _innerStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream.Dispose();
        }
        base.Dispose(disposing);
    }
}
