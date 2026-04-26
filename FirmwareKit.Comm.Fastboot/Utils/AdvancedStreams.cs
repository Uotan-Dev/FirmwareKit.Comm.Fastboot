namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// A decorator stream that provides a view of a slice of another stream.
/// <para>提供另一个流的切片视图的装饰器流。</para>
/// </summary>
public class SubStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _offset;
    private readonly long _length;
    private long _position;

    /// <summary>
    /// Initializes a new SubStream that wraps a portion of the specified base stream.
    /// <para>初始化一个新的 SubStream，包装指定基础流的一部分。</para>
    /// </summary>
    /// <param name="baseStream">The base stream to wrap. <para>要包装的基础流。</para></param>
    /// <param name="offset">The offset in the base stream where the SubStream starts. <para>SubStream 开始的基础流偏移量。</para></param>
    /// <param name="length">The length of the SubStream. <para>SubStream 的长度。</para></param>
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

    /// <summary>
    /// Gets a value indicating whether the current stream supports reading.
    /// <para>获取一个值，指示当前流是否支持读取。</para>
    /// </summary>
    public override bool CanRead => _baseStream.CanRead;
    /// <summary>
    /// Gets a value indicating whether the current stream supports seeking.
    /// <para>获取一个值，指示当前流是否支持查找。</para>
    /// </summary>
    public override bool CanSeek => _baseStream.CanSeek;
    /// <summary>
    /// Gets a value indicating whether the current stream supports writing.
    /// <para>获取一个值，指示当前流是否支持写入。</para>
    /// </summary>
    public override bool CanWrite => false;
    /// <summary>
    /// Gets the length of the current stream.
    /// <para>获取当前流的长度。</para>
    /// </summary>
    public override long Length => _length;

    /// <summary>
    /// Gets or sets the position within the current stream.
    /// <para>获取或设置当前流中的位置。</para>
    /// </summary>
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

    /// <summary>
    /// Flushes the current buffer to the underlying stream.
    /// <para>将当前缓冲区刷新到底层流。</para>
    /// </summary>
    public override void Flush() => _baseStream.Flush();

    /// <summary>
    /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
    /// <para>从当前流读取字节序列，并将流中的位置前进读取的字节数。</para>
    /// </summary>
    /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source. <para>字节数组。当此方法返回时，缓冲区包含指定的字节数组，其中 offset 和 (offset + count - 1) 之间的值被从当前源读取的字节替换。</para></param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream. <para>缓冲区中从零开始的字节偏移量，从此处开始存储从当前流读取的数据。</para></param>
    /// <param name="count">The maximum number of bytes to be read from the current stream. <para>要从当前流读取的最大字节数。</para></param>
    /// <returns>The total number of bytes read into the buffer. <para>读入缓冲区的总字节数。</para></returns>
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

    /// <summary>
    /// Sets the position within the current stream.
    /// <para>设置当前流中的位置。</para>
    /// </summary>
    /// <param name="offset">A byte offset relative to the origin parameter. <para>相对于 origin 参数的字节偏移量。</para></param>
    /// <param name="origin">A value of type SeekOrigin indicating the reference point used to obtain the new position. <para>SeekOrigin 类型的值，指示用于获取新位置的参考点。</para></param>
    /// <returns>The new position within the current stream. <para>当前流中的新位置。</para></returns>
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

    /// <summary>
    /// Sets the length of the current stream.
    /// <para>设置当前流的长度。</para>
    /// </summary>
    /// <param name="value">The desired length of the current stream in bytes. <para>当前流的所需长度（以字节为单位）。</para></param>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <summary>
    /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
    /// <para>将字节序列写入当前流，并将此流中的当前位置前进写入的字节数。</para>
    /// </summary>
    /// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream. <para>字节数组。此方法将 count 字节从缓冲区复制到当前流。</para></param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream. <para>缓冲区中从零开始的字节偏移量，从此处开始将字节复制到当前流。</para></param>
    /// <param name="count">The number of bytes to be written to the current stream. <para>要写入当前流的字节数。</para></param>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// A read-only stream that concatenates multiple streams or repeats data.
/// <para>一个只读流，用于连接多个流或重复数据。</para>
/// </summary>
public class ConcatenatedStream : Stream
{
    private readonly Stream[] _streams;
    private int _currentStreamIndex = 0;
    private long _position = 0;
    private readonly long _totalLength;

    /// <summary>
    /// Initializes a new ConcatenatedStream with the specified streams.
    /// <para>使用指定的流初始化一个新的 ConcatenatedStream。</para>
    /// </summary>
    /// <param name="streams">The streams to concatenate. <para>要连接的流。</para></param>
    public ConcatenatedStream(params Stream[] streams)
    {
        _streams = streams;
        _totalLength = 0;
        foreach (var s in _streams) _totalLength += s.Length;
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports reading.
    /// <para>获取一个值，指示当前流是否支持读取。</para>
    /// </summary>
    public override bool CanRead => true;
    /// <summary>
    /// Gets a value indicating whether the current stream supports seeking.
    /// <para>获取一个值，指示当前流是否支持查找。</para>
    /// </summary>
    public override bool CanSeek => true;
    /// <summary>
    /// Gets a value indicating whether the current stream supports writing.
    /// <para>获取一个值，指示当前流是否支持写入。</para>
    /// </summary>
    public override bool CanWrite => false;
    /// <summary>
    /// Gets the length of the current stream.
    /// <para>获取当前流的长度。</para>
    /// </summary>
    public override long Length => _totalLength;
    /// <summary>
    /// Gets or sets the position within the current stream.
    /// <para>获取或设置当前流中的位置。</para>
    /// </summary>
    public override long Position
    {
        get => _position;
        set { Seek(value, SeekOrigin.Begin); }
    }

    /// <summary>
    /// Flushes the current buffer to the underlying stream.
    /// <para>将当前缓冲区刷新到底层流。</para>
    /// </summary>
    public override void Flush() { }

    /// <summary>
    /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
    /// <para>从当前流读取字节序列，并将流中的位置前进读取的字节数。</para>
    /// </summary>
    /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source. <para>字节数组。当此方法返回时，缓冲区包含指定的字节数组，其中 offset 和 (offset + count - 1) 之间的值被从当前源读取的字节替换。</para></param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream. <para>缓冲区中从零开始的字节偏移量，从此处开始存储从当前流读取的数据。</para></param>
    /// <param name="count">The maximum number of bytes to be read from the current stream. <para>要从当前流读取的最大字节数。</para></param>
    /// <returns>The total number of bytes read into the buffer. <para>读入缓冲区的总字节数。</para></returns>
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

    /// <summary>
    /// Sets the position within the current stream.
    /// <para>设置当前流中的位置。</para>
    /// </summary>
    /// <param name="offset">A byte offset relative to the origin parameter. <para>相对于 origin 参数的字节偏移量。</para></param>
    /// <param name="origin">A value of type SeekOrigin indicating the reference point used to obtain the new position. <para>SeekOrigin 类型的值，指示用于获取新位置的参考点。</para></param>
    /// <returns>The new position within the current stream. <para>当前流中的新位置。</para></returns>
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

    /// <summary>
    /// Sets the length of the current stream.
    /// <para>设置当前流的长度。</para>
    /// </summary>
    /// <param name="value">The desired length of the current stream in bytes. <para>当前流的所需长度（以字节为单位）。</para></param>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <summary>
    /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
    /// <para>将字节序列写入当前流，并将此流中的当前位置前进写入的字节数。</para>
    /// </summary>
    /// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream. <para>字节数组。此方法将 count 字节从缓冲区复制到当前流。</para></param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream. <para>缓冲区中从零开始的字节偏移量，从此处开始将字节复制到当前流。</para></param>
    /// <param name="count">The number of bytes to be written to the current stream. <para>要写入当前流的字节数。</para></param>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// Releases the unmanaged resources used by the ConcatenatedStream and optionally releases the managed resources.
    /// <para>释放 ConcatenatedStream 使用的非托管资源，并可选地释放托管资源。</para>
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources. <para>true 表示释放托管和非托管资源；false 表示仅释放非托管资源。</para></param>
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
/// <para>一个流，重复固定字节序列以填充特定长度。</para>
/// </summary>
public class PaddingStream : Stream
{
    private readonly long _length;
    private readonly byte _paddingByte;
    private long _position;

    /// <summary>
    /// Initializes a new PaddingStream with the specified length and padding byte.
    /// <para>使用指定的长度和填充字节初始化一个新的 PaddingStream。</para>
    /// </summary>
    /// <param name="length">The length of the padding stream. <para>填充流的长度。</para></param>
    /// <param name="paddingByte">The byte to use for padding. <para>用于填充的字节。</para></param>
    public PaddingStream(long length, byte paddingByte = 0)
    {
        _length = length;
        _paddingByte = paddingByte;
        _position = 0;
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports reading.
    /// <para>获取一个值，指示当前流是否支持读取。</para>
    /// </summary>
    public override bool CanRead => true;
    /// <summary>
    /// Gets a value indicating whether the current stream supports seeking.
    /// <para>获取一个值，指示当前流是否支持查找。</para>
    /// </summary>
    public override bool CanSeek => true;
    /// <summary>
    /// Gets a value indicating whether the current stream supports writing.
    /// <para>获取一个值，指示当前流是否支持写入。</para>
    /// </summary>
    public override bool CanWrite => false;
    /// <summary>
    /// Gets the length of the current stream.
    /// <para>获取当前流的长度。</para>
    /// </summary>
    public override long Length => _length;
    /// <summary>
    /// Gets or sets the position within the current stream.
    /// <para>获取或设置当前流中的位置。</para>
    /// </summary>
    public override long Position { get => _position; set => _position = value; }

    /// <summary>
    /// Flushes the current buffer to the underlying stream.
    /// <para>将当前缓冲区刷新到底层流。</para>
    /// </summary>
    public override void Flush() { }

    /// <summary>
    /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
    /// <para>从当前流读取字节序列，并将流中的位置前进读取的字节数。</para>
    /// </summary>
    /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source. <para>字节数组。当此方法返回时，缓冲区包含指定的字节数组，其中 offset 和 (offset + count - 1) 之间的值被从当前源读取的字节替换。</para></param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream. <para>缓冲区中从零开始的字节偏移量，从此处开始存储从当前流读取的数据。</para></param>
    /// <param name="count">The maximum number of bytes to be read from the current stream. <para>要从当前流读取的最大字节数。</para></param>
    /// <returns>The total number of bytes read into the buffer. <para>读入缓冲区的总字节数。</para></returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        long remaining = _length - _position;
        if (remaining <= 0) return 0;
        int toRead = (int)Math.Min(count, remaining);
        for (int i = 0; i < toRead; i++) buffer[offset + i] = _paddingByte;
        _position += toRead;
        return toRead;
    }

    /// <summary>
    /// Sets the position within the current stream.
    /// <para>设置当前流中的位置。</para>
    /// </summary>
    /// <param name="offset">A byte offset relative to the origin parameter. <para>相对于 origin 参数的字节偏移量。</para></param>
    /// <param name="origin">A value of type SeekOrigin indicating the reference point used to obtain the new position. <para>SeekOrigin 类型的值，指示用于获取新位置的参考点。</para></param>
    /// <returns>The new position within the current stream. <para>当前流中的新位置。</para></returns>
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

    /// <summary>
    /// Sets the length of the current stream.
    /// <para>设置当前流的长度。</para>
    /// </summary>
    /// <param name="value">The desired length of the current stream in bytes. <para>当前流的所需长度（以字节为单位）。</para></param>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <summary>
    /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
    /// <para>将字节序列写入当前流，并将此流中的当前位置前进写入的字节数。</para>
    /// </summary>
    /// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream. <para>字节数组。此方法将 count 字节从缓冲区复制到当前流。</para></param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream. <para>缓冲区中从零开始的字节偏移量，从此处开始将字节复制到当前流。</para></param>
    /// <param name="count">The number of bytes to be written to the current stream. <para>要写入当前流的字节数。</para></param>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
