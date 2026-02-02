using System.IO.Compression;

namespace KeSpider.ZipEncodingDetector;

sealed class ZipEntryNameStream(IEnumerable<ZipArchiveEntry> entries) : Stream
{
    private readonly IEnumerator<ZipArchiveEntry> enumerator
        = entries.GetEnumerator();
    private byte[]? bytes;
    private int offset;
    private bool disposed;
    public override bool CanRead => !disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        while (bytes is null || bytes.Length == 0)
        {
            if (!enumerator.MoveNext())
                return 0;
            bytes = enumerator.Current.GetRawEntryName();
            offset = 0;
        }
        ReadOnlySpan<byte> slice = bytes.AsSpan(offset);
        int bufferLength = buffer.Length;
        if (slice.Length <= bufferLength)
        {
            slice.CopyTo(buffer);
            bytes = null;
            return slice.Length;
        }
        else
        {
            offset += bufferLength;
            slice[..bufferLength].CopyTo(buffer);
            return bufferLength;
        }
    }
    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        while (bytes is null || bytes.Length == 0)
        {
            if (!enumerator.MoveNext())
                return -1;
            bytes = enumerator.Current.GetRawEntryName();
            offset = 0;
        }
        ReadOnlySpan<byte> slice = bytes.AsSpan(offset);
        if (slice.Length > 1)
            offset++;
        else
            bytes = null;
        return slice[0];
    }
    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();
    public override void SetLength(long value)
        => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();
    public override void Write(ReadOnlySpan<byte> buffer)
        => throw new NotSupportedException();
    public override void WriteByte(byte value)
        => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        enumerator.Dispose();
        bytes = null;
        disposed = true;
        base.Dispose(disposing);
    }
}
