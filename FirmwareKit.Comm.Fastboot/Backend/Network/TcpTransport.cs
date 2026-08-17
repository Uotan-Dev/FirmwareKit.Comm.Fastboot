using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace FirmwareKit.Comm.Fastboot.Network;

/// <summary>
/// Fastboot over TCP transport, implementing the AOSP TCP fastboot protocol with handshake.
/// <para>基于 TCP 的 Fastboot 传输，实现 AOSP TCP fastboot 协议及握手。</para>
/// </summary>
public class TcpTransport : IFastbootBufferedTransport
{
    private const int DefaultIoTimeoutMs = 10000;
    private const int HandshakeTimeoutMs = 30000;
    private readonly TcpClient _client = new();
    private readonly object _ioLock = new();
    private readonly byte[] _readLenBuffer = new byte[8];
    private readonly byte[] _writeLenBuffer = new byte[8];
    private NetworkStream? _stream;
    private ulong _messageBytesLeft = 0;

    /// <summary>
    /// Gets the host address of the TCP connection.
    /// <para>获取 TCP 连接的主机地址。</para>
    /// </summary>
    public string Host { get; }

    /// <summary>
    /// Gets the port number of the TCP connection.
    /// <para>获取 TCP 连接的端口号。</para>
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Initializes a new TcpTransport and performs the fastboot TCP handshake.
    /// <para>初始化新的 TcpTransport 并执行 fastboot TCP 握手。</para>
    /// </summary>
    public TcpTransport(string host, int port = 5554)
    {
        Host = host;
        Port = port;
        InitializeProtocol();
    }

    private void InitializeProtocol()
    {
        _client.ReceiveTimeout = DefaultIoTimeoutMs;
        _client.SendTimeout = DefaultIoTimeoutMs;
        Task connectTask = _client.ConnectAsync(Host, Port);
        if (!connectTask.Wait(HandshakeTimeoutMs))
        {
            throw new Exception($"Handshake failed: connect timeout after {HandshakeTimeoutMs} ms.");
        }
        if (connectTask.IsFaulted)
        {
            throw connectTask.Exception?.GetBaseException() ?? new Exception("Handshake failed: connect failed.");
        }
        _stream = _client.GetStream();
        _stream.ReadTimeout = DefaultIoTimeoutMs;
        _stream.WriteTimeout = DefaultIoTimeoutMs;
        byte[] handshake = Encoding.ASCII.GetBytes("FB01");
        _stream.Write(handshake, 0, handshake.Length);

        byte[] response = new byte[4];
        int read = ReadFully(response, 0, 4);
        if (read != 4)
        {
            throw new Exception("Handshake failed: unexpected response or timeout.");
        }

        string responseText = Encoding.ASCII.GetString(response);
        if (!responseText.StartsWith("FB", StringComparison.Ordinal))
        {
            throw new Exception("Handshake failed: unrecognized initialization message.");
        }

        string versionStr = responseText.Substring(2, 2);
        if (!int.TryParse(versionStr, out int version) || version < 1)
        {
            throw new Exception($"Handshake failed: unknown TCP protocol version {versionStr} (host version 01).");
        }
    }

    private int ReadFully(byte[] buffer, int offset, int length)
    {
        if (_stream == null) throw new InvalidOperationException("Stream not initialized");
        int totalRead = 0;
        while (totalRead < length)
        {
            int read = _stream.Read(buffer, offset + totalRead, length - totalRead);
            if (read <= 0) break;
            totalRead += read;
        }
        return totalRead;
    }

    /// <summary>
    /// Reads data from the TCP transport with the specified maximum length.
    /// <para>从 TCP 传输层读取指定最大长度的数据。</para>
    /// </summary>
    public byte[] Read(int length)
    {
        if (length <= 0) return Array.Empty<byte>();

        byte[] dataBuffer = new byte[length];
        int actuallyRead = ReadInto(dataBuffer, 0, length);
        if (actuallyRead < length)
        {
            Array.Resize(ref dataBuffer, actuallyRead);
        }
        return dataBuffer;
    }

    /// <summary>
    /// Reads data directly into the specified buffer for zero-allocation reads.
    /// <para>将数据直接读入指定缓冲区，实现零分配读取。</para>
    /// </summary>
    public int ReadInto(byte[] buffer, int offset, int length)
    {
        if (length <= 0) return 0;
        if (offset < 0 || length < 0 || offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        lock (_ioLock)
        {
            if (_messageBytesLeft == 0)
            {
                if (ReadFully(_readLenBuffer, 0, 8) != 8)
                {
                    throw new Exception("Failed to read message length from TCP stream.");
                }
                // AOSP TCP fastboot frames the payload length as a big-endian unsigned 64-bit
                // integer. The wire-level DATA size field is 32-bit, so any frame larger than
                // uint.MaxValue is invalid and would overflow the int cast below.
                ulong frameLength = BinaryPrimitives.ReadUInt64BigEndian(_readLenBuffer);
                if (frameLength > uint.MaxValue)
                {
                    throw new Exception($"Invalid TCP frame length: {frameLength} (exceeds uint.MaxValue).");
                }
                _messageBytesLeft = frameLength;
            }

            int toRead = (int)Math.Min((ulong)length, _messageBytesLeft);
            int actuallyRead = ReadFully(buffer, offset, toRead);
            _messageBytesLeft -= (ulong)actuallyRead;
            return actuallyRead;
        }
    }

    /// <summary>
    /// Writes data to the TCP transport with the specified length.
    /// <para>向 TCP 传输层写入指定长度的数据。</para>
    /// </summary>
    public long Write(byte[] data, int length)
    {
        if (_stream == null) throw new InvalidOperationException("Stream not initialized");
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        lock (_ioLock)
        {
            BinaryPrimitives.WriteUInt64BigEndian(_writeLenBuffer, (ulong)length);

            _stream.Write(_writeLenBuffer, 0, 8);
            _stream.Write(data, 0, length);
            _stream.Flush();
            return length;
        }
    }

    /// <summary>
    /// Disposes the TCP client and stream resources.
    /// <para>释放 TCP 客户端和流资源。</para>
    /// </summary>
    public void Dispose()
    {
        _stream?.Dispose();
        _client?.Dispose();
    }

}
