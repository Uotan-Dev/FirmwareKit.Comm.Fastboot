using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FirmwareKit.Comm.Fastboot.Backend.Network;

public class TcpTransport : IFastbootTransport
{
    private const int DefaultIoTimeoutMs = 30000;
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;
    private long _messageBytesLeft = 0;

    public string Host { get; }
    public int Port { get; }

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
        if (!connectTask.Wait(DefaultIoTimeoutMs))
        {
            throw new Exception($"Handshake failed: connect timeout after {DefaultIoTimeoutMs} ms.");
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

    public byte[] Read(int length)
    {
        if (_messageBytesLeft == 0)
        {
            byte[] lenBuffer = new byte[8];
            if (ReadFully(lenBuffer, 0, 8) != 8)
            {
                throw new Exception("Failed to read message length from TCP stream.");
            }
            _messageBytesLeft = BinaryPrimitives.ReadInt64BigEndian(lenBuffer);
        }

        int toRead = (int)Math.Min(length, _messageBytesLeft);
        byte[] dataBuffer = new byte[toRead];
        int actuallyRead = ReadFully(dataBuffer, 0, toRead);
        if (actuallyRead < toRead)
        {
            Array.Resize(ref dataBuffer, actuallyRead);
        }
        _messageBytesLeft -= actuallyRead;
        return dataBuffer;
    }

    public long Write(byte[] data, int length)
    {
        if (_stream == null) throw new InvalidOperationException("Stream not initialized");
        byte[] header = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(header, length);

        _stream.Write(header, 0, 8);
        _stream.Write(data, 0, length);
        _stream.Flush();
        return length;
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _client?.Dispose();
    }


}
