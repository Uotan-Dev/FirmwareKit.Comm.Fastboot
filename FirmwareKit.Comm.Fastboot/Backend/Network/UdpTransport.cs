using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace FirmwareKit.Comm.Fastboot.Network;
/// <summary>
/// Fastboot over UDP Transport.
/// Fully implements the AOSP Fastboot over Network protocol (headers, sequence numbers, handshake).
/// <para>基于 UDP 的 Fastboot 传输。
/// 完整实现 AOSP Fastboot 网络协议（头部、序列号、握手）。</para>
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

    /// <summary>
    /// Gets the host address of the UDP connection.
    /// <para>获取 UDP 连接的主机地址。</para>
    /// </summary>
    public string Host { get; }

    /// <summary>
    /// Gets the port number of the UDP connection.
    /// <para>获取 UDP 连接的端口号。</para>
    /// </summary>
    public int Port { get; }
    private readonly int _maxTransmissionAttempts;

    /// <summary>
    /// Initializes a new UdpTransport and performs the fastboot UDP handshake.
    /// <para>初始化新的 UdpTransport 并执行 fastboot UDP 握手。</para>
    /// </summary>
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
        List<byte> fullResponse = [];

        ExchangePacketSequence(
            id,
            flag,
            seq,
            txData,
            txOffset,
            txLen,
            attempts,
            fullResponse,
            requireEmptyPayload: false,
            output: null,
            outputOffset: 0,
            outputLength: 0,
            out nextSeq,
            out _);

        return fullResponse.ToArray();
    }

    /// <summary>
    /// Reads data from the UDP transport with the specified maximum length.
    /// <para>从 UDP 传输层读取指定最大长度的数据。</para>
    /// </summary>
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

        int read = ReceiveFastbootInto(buffer, offset, length, _maxTransmissionAttempts, out ushort nextSeq);
        _sequence = nextSeq;
        return read;
    }

    private int ReceiveFastbootInto(byte[] output, int outputOffset, int outputLength, int attempts, out ushort nextSeq)
    {
        ExchangePacketSequence(
            PacketId.Fastboot,
            PacketFlag.None,
            (ushort)_sequence,
            Array.Empty<byte>(),
            0,
            0,
            attempts,
            collector: null,
            requireEmptyPayload: false,
            output,
            outputOffset,
            outputLength,
            out nextSeq,
            out int written);

        return written;
    }

    /// <summary>
    /// Writes data to the UDP transport with the specified length.
    /// <para>向 UDP 传输层写入指定长度的数据。</para>
    /// </summary>
    public long Write(byte[] data, int length)
    {
        SendFastbootNoPayload(PacketId.Fastboot, data, length, _maxTransmissionAttempts);
        return length;
    }
    private void SendFastbootNoPayload(PacketId id, byte[] txData, int txLength, int attempts)
    {
        int offset = 0;

        do
        {
            int chunkLen = Math.Min(txLength - offset, _maxDataLength);
            PacketFlag flag = (offset + chunkLen < txLength) ? PacketFlag.Continuation : PacketFlag.None;

            SendSinglePacketNoPayload(id, (ushort)_sequence, flag, txData, offset, chunkLen, attempts, out ushort nextSeq);

            _sequence = nextSeq;
            offset += chunkLen;
        } while (offset < txLength);
    }

    private void SendSinglePacketNoPayload(PacketId id, ushort seq, PacketFlag flag, byte[] txData, int txOffset, int txLen, int attempts, out ushort nextSeq)
    {
        ExchangePacketSequence(
            id,
            flag,
            seq,
            txData,
            txOffset,
            txLen,
            attempts,
            collector: null,
            requireEmptyPayload: true,
            output: null,
            outputOffset: 0,
            outputLength: 0,
            out nextSeq,
            out _);
    }

    private void ExchangePacketSequence(
        PacketId id,
        PacketFlag flag,
        ushort seq,
        byte[] txData,
        int txOffset,
        int txLen,
        int attempts,
        List<byte>? collector,
        bool requireEmptyPayload,
        byte[]? output,
        int outputOffset,
        int outputLength,
        out ushort nextSeq,
        out int written)
    {
        PacketId currentId = id;
        PacketFlag currentFlag = flag;
        int currentTxOffset = txOffset;
        int currentTxLen = txLen;
        ushort currentSeq = seq;
        written = 0;

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

                        int payloadLen = rxPacket.Length - HeaderSize;
                        if (payloadLen > 0)
                        {
                            if (requireEmptyPayload)
                            {
                                throw new Exception("UDP protocol error: target sent fastboot data out-of-turn.");
                            }

                            if (output != null)
                            {
                                if (written + payloadLen > outputLength)
                                {
                                    throw new Exception("UDP protocol error: receive overflow, target sent too much fastboot data.");
                                }

                                Buffer.BlockCopy(rxPacket, HeaderSize, output, outputOffset + written, payloadLen);
                                written += payloadLen;
                            }
                            else if (collector != null)
                            {
                                for (int j = HeaderSize; j < rxPacket.Length; j++)
                                {
                                    collector.Add(rxPacket[j]);
                                }
                            }
                        }

                        gotValidResponse = true;
                        currentSeq = (ushort)((currentSeq + 1) & 0xFFFF);

                        bool continuation = (rxPacket[1] & (byte)PacketFlag.Continuation) != 0;
                        if (!continuation)
                        {
                            nextSeq = currentSeq;
                            return;
                        }

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

    /// <summary>
    /// Disposes the UDP client resources.
    /// <para>释放 UDP 客户端资源。</para>
    /// </summary>
    public void Dispose()
    {
        _client?.Dispose();
    }
}

