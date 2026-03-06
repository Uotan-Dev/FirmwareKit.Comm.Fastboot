namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// A decorator stream that provides a view of a slice of another stream.
/// </summary>
public class SubStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _offset;
    private readonly long _length;
    private long _position;

    public SubStream(Stream baseStream, long offset, long length)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        _offset = offset;
        _length = length;
        _position = 0;

        if (_baseStream.CanSeek)
        {
            if (_offset + _length > _baseStream.Length)
                throw new ArgumentException("SubStream range exceeds base stream length.");
        }
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set
        {
            if (!CanSeek) throw new NotSupportedException();
            if (value < 0 || value > _length) throw new ArgumentOutOfRangeException(nameof(value));
            _position = value;
        }
    }

    public override void Flush() => _baseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remaining = _length - _position;
        if (remaining <= 0) return 0;

        int toRead = (int)Math.Min(count, remaining);
        if (CanSeek)
        {
            _baseStream.Seek(_offset + _position, SeekOrigin.Begin);
        }

        int read = _baseStream.Read(buffer, offset, toRead);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!CanSeek) throw new NotSupportedException();
        long newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPos < 0 || newPos > _length) throw new ArgumentOutOfRangeException(nameof(newPos));
        _position = newPos;
        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// A read-only stream that concatenates multiple streams or repeats data.
/// </summary>
public class ConcatenatedStream : Stream
{
    private readonly Stream[] _streams;
    private int _currentStreamIndex = 0;
    private long _position = 0;
    private readonly long _totalLength;

    public ConcatenatedStream(params Stream[] streams)
    {
        _streams = streams;
        _totalLength = 0;
        foreach (var s in _streams) _totalLength += s.Length;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _totalLength;
    public override long Position
    {
        get => _position;
        set { Seek(value, SeekOrigin.Begin); }
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_currentStreamIndex >= _streams.Length) return 0;

        int totalRead = 0;
        while (count > 0 && _currentStreamIndex < _streams.Length)
        {
            int read = _streams[_currentStreamIndex].Read(buffer, offset, count);
            if (read == 0)
            {
                _currentStreamIndex++;
                continue;
            }
            totalRead += read;
            _position += read;
            offset += read;
            count -= read;
        }
        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _totalLength + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPos < 0 || newPos > _totalLength) throw new ArgumentOutOfRangeException(nameof(newPos));

        _position = newPos;
        long cumulative = 0;
        for (int i = 0; i < _streams.Length; i++)
        {
            if (newPos >= cumulative && newPos < cumulative + _streams[i].Length)
            {
                _currentStreamIndex = i;
                _streams[i].Seek(newPos - cumulative, SeekOrigin.Begin);
                // Reset other streams if necessary? Usually not needed for read-only.
                return _position;
            }
            cumulative += _streams[i].Length;
        }
        _currentStreamIndex = _streams.Length; // EOF
        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var s in _streams) s.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// A stream that repeats a fixed byte sequence to fill a specific length.
/// </summary>
public class PaddingStream : Stream
{
    private readonly long _length;
    private readonly byte _paddingByte;
    private long _position;

    public PaddingStream(long length, byte paddingByte = 0)
    {
        _length = length;
        _paddingByte = paddingByte;
        _position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _position; set => _position = value; }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remaining = _length - _position;
        if (remaining <= 0) return 0;
        int toRead = (int)Math.Min(count, remaining);
        for (int i = 0; i < toRead; i++) buffer[offset + i] = _paddingByte;
        _position += toRead;
        return toRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        _position = Math.Max(0, Math.Min(newPos, _length));
        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
