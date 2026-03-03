using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace FirmwareKit.Comm.Fastboot
{
    public class TcpTransport : IFastbootTransport
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private long _messageBytesLeft = 0;

        public string Host { get; }
        public int Port { get; }

        public TcpTransport(string host, int port = 5554)
        {
            Host = host;
            Port = port;
            _client = new TcpClient();
            _client.Connect(host, port);
            _stream = _client.GetStream();

            InitializeProtocol();
        }

        private void InitializeProtocol()
        {
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

            if (!int.TryParse(responseText.Substring(2, 2), out int version) || version < 1)
            {
                throw new Exception($"Handshake failed: unknown TCP protocol version {responseText.Substring(2, 2)} (host version 01).");
            }
        }

        private int ReadFully(byte[] buffer, int offset, int length)
        {
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
}
