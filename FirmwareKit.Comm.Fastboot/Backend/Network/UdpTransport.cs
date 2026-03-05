using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace FirmwareKit.Comm.Fastboot.Backend.Network;
/// <summary>
/// Fastboot over UDP Transport
/// Fully implements the AOSP Fastboot over Network protocol (headers, sequence numbers, handshake)
/// </summary>
public class UdpTransport(string host, int port = 5554) : IFastbootTransport
{
    private readonly UdpClient _client = new();
    private readonly IPEndPoint _endpoint = new(IPAddress.Parse(host), port);
    private int _sequence = 0;
    private int _maxDataLength = 512 - 4;
    private readonly int _timeoutMs = 1000;
    private const int HeaderSize = 4;
    private readonly List<byte> _readBuffer = [];

    private enum PacketId : byte
    {
        Error = 0x00,
        DeviceQuery = 0x01,
        Initialization = 0x02,
        Fastboot = 0x03
    }

    private enum PacketFlag : byte
    {
        None = 0x00,
        Continuation = 0x01
    }

    public string Host { get; } = host;
    public int Port { get; } = port;
    private const int MaxTransmissionAttempts = 10;

    private void InitializeProtocol()
    {
        _client.Client.ReceiveTimeout = _timeoutMs;
        _client.Client.SendTimeout = _timeoutMs;

        byte[] response = SendSinglePacket(PacketId.DeviceQuery, 0, PacketFlag.None, [], 0, 0, MaxTransmissionAttempts);
        if (response.Length < 2) throw new Exception("Invalid query response from target.");
        _sequence = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2));
        byte[] initData = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(initData.AsSpan(0, 2), 0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(initData.AsSpan(2, 2), 512);
        response = SendSinglePacket(PacketId.Initialization, (ushort)_sequence, PacketFlag.None, initData, initData.Length, MaxTransmissionAttempts);
        if (response.Length < 4) throw new Exception("Invalid initialization response from target.");

        ushort version = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2));
        ushort packetSize = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2, 2));

        if (version < 1) throw new Exception($"Target reported invalid protocol version {version}");

        _maxDataLength = packetSize - HeaderSize;
        _sequence = (_sequence + 1) & 0xFFFF;
    }

    private byte[] SendDataInternal(PacketId id, byte[] txData, int txLength, int attempts)
    {
        int offset = 0;
        List<byte> fullResponse = new List<byte>();

        do
        {
            int chunkLen = Math.Min(txLength - offset, _maxDataLength);
            PacketFlag flag = (offset + chunkLen < txLength) ? PacketFlag.Continuation : PacketFlag.None;

            byte[] rxData = SendSinglePacket(id, (ushort)_sequence, flag, txData, offset, chunkLen, attempts);
            fullResponse.AddRange(rxData);

            _sequence = (_sequence + 1) & 0xFFFF;
            offset += chunkLen;
        } while (offset < txLength);

        return fullResponse.ToArray();
    }

    private byte[] SendSinglePacket(PacketId id, ushort seq, PacketFlag flag, byte[] txData, int txLen, int attempts)
    {
        return SendSinglePacket(id, seq, flag, txData, 0, txLen, attempts);
    }

    private byte[] SendSinglePacket(PacketId id, ushort seq, PacketFlag flag, byte[] txData, int txOffset, int txLen, int attempts)
    {
        byte[] packet = new byte[HeaderSize + txLen];
        packet[0] = (byte)id;
        packet[1] = (byte)flag;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), seq);
        if (txLen > 0) Array.Copy(txData, txOffset, packet, HeaderSize, txLen);

        for (int i = 0; i < attempts; i++)
        {
            _client.Send(packet, packet.Length, _endpoint);

            try
            {
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                byte[] rxPacket = _client.Receive(ref from);
                if (rxPacket.Length < HeaderSize) continue;
                if (BinaryPrimitives.ReadUInt16BigEndian(rxPacket.AsSpan(2, 2)) == seq &&
                    (rxPacket[0] == (byte)id || rxPacket[0] == (byte)PacketId.Error))
                {
                    if (rxPacket[0] == (byte)PacketId.Error)
                    {
                        throw new Exception("Target returned error response.");
                    }

                    byte[] rxData = new byte[rxPacket.Length - HeaderSize];
                    Array.Copy(rxPacket, HeaderSize, rxData, 0, rxData.Length);
                    return rxData;
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                continue;
            }
        }
        throw new Exception($"Failed to receive response after {attempts} attempts.");
    }

    public byte[] Read(int length)
    {
        if (_readBuffer.Count == 0)
        {
            return Array.Empty<byte>();
        }

        int toCopy = Math.Min(length, _readBuffer.Count);
        byte[] result = _readBuffer.Take(toCopy).ToArray();
        _readBuffer.RemoveRange(0, toCopy);
        return result;
    }

    public long Write(byte[] data, int length)
    {
        byte[] response = SendDataInternal(PacketId.Fastboot, data, length, MaxTransmissionAttempts);
        _readBuffer.AddRange(response);
        return length;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

