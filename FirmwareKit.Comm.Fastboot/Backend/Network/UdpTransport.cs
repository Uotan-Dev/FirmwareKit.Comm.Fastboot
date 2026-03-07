using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace FirmwareKit.Comm.Fastboot.Network;
/// <summary>
/// Fastboot over UDP Transport
/// Fully implements the AOSP Fastboot over Network protocol (headers, sequence numbers, handshake)
/// </summary>
public class UdpTransport : IFastbootBufferedTransport
{
    private readonly UdpClient _client;
    private readonly IPEndPoint _endpoint;
    private int _sequence = 0;
    private int _maxDataLength = 512 - 4;
    private readonly int _timeoutMs;
    private const int HeaderSize = 4;
    private const int HostMaxPacketSize = 512;

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

    public string Host { get; }
    public int Port { get; }
    private readonly int _maxTransmissionAttempts;

    public UdpTransport(string host, int port = 5554, int timeoutMs = 1000, int maxTransmissionAttempts = 10)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        if (maxTransmissionAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(maxTransmissionAttempts));

        Host = host;
        Port = port;
        _timeoutMs = timeoutMs;
        _maxTransmissionAttempts = maxTransmissionAttempts;
        _client = new UdpClient();
        _endpoint = new IPEndPoint(IPAddress.Parse(host), port);

        InitializeProtocol();
    }

    private void InitializeProtocol()
    {
        _client.Client.ReceiveTimeout = _timeoutMs;
        _client.Client.SendTimeout = _timeoutMs;

        // Handshake runs at transport creation and is sensitive to scheduling jitter
        // in constrained CI environments. Use a slightly larger retry budget here
        // without changing steady-state transfer behavior.
        int initAttempts = Math.Max(_maxTransmissionAttempts, 5);

        byte[] response = SendSinglePacket(PacketId.DeviceQuery, 0, PacketFlag.None, [], 0, 0, initAttempts, out _);
        if (response.Length < 2) throw new Exception("Invalid query response from target.");
        _sequence = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2));
        byte[] initData = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(initData.AsSpan(0, 2), 0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(initData.AsSpan(2, 2), HostMaxPacketSize);
        response = SendSinglePacket(PacketId.Initialization, (ushort)_sequence, PacketFlag.None, initData, initData.Length, initAttempts, out _);
        if (response.Length < 4) throw new Exception("Invalid initialization response from target.");

        ushort version = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(0, 2));
        ushort packetSize = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2, 2));

        if (version < 1) throw new Exception($"Target reported invalid protocol version {version}");
        if (packetSize < HostMaxPacketSize) throw new Exception($"Target reported invalid packet size {packetSize}");

        packetSize = (ushort)Math.Min(HostMaxPacketSize, (int)packetSize);
        _maxDataLength = packetSize - HeaderSize;
        _sequence = (_sequence + 1) & 0xFFFF;
    }

    private byte[] SendDataInternal(PacketId id, byte[] txData, int txLength, int attempts)
    {
        int offset = 0;
        List<byte> fullResponse = [];

        do
        {
            int chunkLen = Math.Min(txLength - offset, _maxDataLength);
            PacketFlag flag = (offset + chunkLen < txLength) ? PacketFlag.Continuation : PacketFlag.None;

            byte[] rxData = SendSinglePacket(id, (ushort)_sequence, flag, txData, offset, chunkLen, attempts, out ushort nextSeq);
            fullResponse.AddRange(rxData);

            _sequence = nextSeq;
            offset += chunkLen;
        } while (offset < txLength);

        return fullResponse.ToArray();
    }

    private byte[] SendSinglePacket(PacketId id, ushort seq, PacketFlag flag, byte[] txData, int txLen, int attempts, out ushort nextSeq)
    {
        return SendSinglePacket(id, seq, flag, txData, 0, txLen, attempts, out nextSeq);
    }

    private byte[] SendSinglePacket(PacketId id, ushort seq, PacketFlag flag, byte[] txData, int txOffset, int txLen, int attempts, out ushort nextSeq)
    {
        PacketId currentId = id;
        PacketFlag currentFlag = flag;
        int currentTxOffset = txOffset;
        int currentTxLen = txLen;
        ushort currentSeq = seq;
        List<byte> fullResponse = [];

        while (true)
        {
            byte[] packet = new byte[HeaderSize + currentTxLen];
            packet[0] = (byte)currentId;
            packet[1] = (byte)currentFlag;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), currentSeq);
            if (currentTxLen > 0) Array.Copy(txData, currentTxOffset, packet, HeaderSize, currentTxLen);

            bool gotValidResponse = false;
            for (int i = 0; i < attempts; i++)
            {
                _client.Send(packet, packet.Length, _endpoint);

                try
                {
                    while (true)
                    {
                        IPEndPoint from = new(IPAddress.Any, 0);
                        byte[] rxPacket = _client.Receive(ref from);
                        if (rxPacket.Length < HeaderSize) continue;

                        ushort responseSeq = BinaryPrimitives.ReadUInt16BigEndian(rxPacket.AsSpan(2, 2));
                        byte responseId = rxPacket[0];
                        if (responseSeq != currentSeq) continue;
                        if (responseId != (byte)currentId && responseId != (byte)PacketId.Error) continue;

                        if (responseId == (byte)PacketId.Error)
                        {
                            throw new Exception("Target returned error response.");
                        }

                        if (rxPacket.Length > HeaderSize)
                        {
                            for (int j = HeaderSize; j < rxPacket.Length; j++)
                            {
                                fullResponse.Add(rxPacket[j]);
                            }
                        }

                        gotValidResponse = true;
                        currentSeq = (ushort)((currentSeq + 1) & 0xFFFF);

                        bool continuation = (rxPacket[1] & (byte)PacketFlag.Continuation) != 0;
                        if (!continuation)
                        {
                            nextSeq = currentSeq;
                            return fullResponse.ToArray();
                        }

                        // Prompt the target for the next continuation fragment.
                        currentId = (PacketId)responseId;
                        currentFlag = PacketFlag.None;
                        currentTxOffset = 0;
                        currentTxLen = 0;
                        break;
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    continue;
                }

                if (gotValidResponse)
                {
                    break;
                }
            }

            if (!gotValidResponse)
            {
                throw new Exception($"Failed to receive response after {attempts} attempts.");
            }
        }
    }

    public byte[] Read(int length)
    {
        if (length <= 0) return Array.Empty<byte>();
        byte[] buffer = new byte[length];
        int read = ReadInto(buffer, 0, length);
        if (read < length)
        {
            Array.Resize(ref buffer, read);
        }
        return buffer;
    }

    public int ReadInto(byte[] buffer, int offset, int length)
    {
        if (length <= 0) return 0;
        if (offset < 0 || length < 0 || offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        byte[] response = SendDataInternal(PacketId.Fastboot, [], 0, _maxTransmissionAttempts);
        if (response.Length > length)
        {
            throw new Exception("UDP protocol error: receive overflow, target sent too much fastboot data.");
        }

        Buffer.BlockCopy(response, 0, buffer, offset, response.Length);
        return response.Length;
    }

    public long Write(byte[] data, int length)
    {
        byte[] response = SendDataInternal(PacketId.Fastboot, data, length, _maxTransmissionAttempts);
        if (response.Length > 0)
        {
            throw new Exception("UDP protocol error: target sent fastboot data out-of-turn.");
        }
        return length;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

