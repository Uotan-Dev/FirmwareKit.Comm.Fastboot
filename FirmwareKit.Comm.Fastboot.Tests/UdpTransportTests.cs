using FirmwareKit.Comm.Fastboot.Network;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class UdpTransportTests
    {
        // 增加超时时间，允许通过环境变量调整，默认1000ms
        private static int TestTimeoutMs => int.TryParse(Environment.GetEnvironmentVariable("UDP_TEST_TIMEOUT_MS"), out var v) ? v : 1000;
        private static int TestMaxAttempts => int.TryParse(Environment.GetEnvironmentVariable("UDP_TEST_ATTEMPTS"), out var v) ? v : 10;

        private const byte IdError = 0x00;
        private const byte IdDeviceQuery = 0x01;
        private const byte IdInitialization = 0x02;
        private const byte IdFastboot = 0x03;

        private static int GetFreeUdpPort()
        {
            // 设置端口复用，避免端口占用冲突
            var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            udp.Dispose();
            return port;
        }

        private static IPEndPoint CompleteHandshake(UdpClient server, ushort version = 1)
        {
            IPEndPoint remote = new(IPAddress.Loopback, 0);

            bool initialized = false;
            int attempts = 0;
            while (!initialized && attempts++ < 20)
            {
                try
                {
                    byte[] packet = server.Receive(ref remote);
                    if (packet.Length < 4)
                        continue;
                    ushort seq = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(2, 2));
                    if (packet[0] == IdDeviceQuery)
                    {
                        byte[] qResp = new byte[6];
                        qResp[0] = IdDeviceQuery;
                        qResp[1] = 0x00;
                        BinaryPrimitives.WriteUInt16BigEndian(qResp.AsSpan(2, 2), seq);
                        BinaryPrimitives.WriteUInt16BigEndian(qResp.AsSpan(4, 2), 0);
                        server.Send(qResp, qResp.Length, remote);
                        continue;
                    }
                    if (packet[0] == IdInitialization)
                    {
                        byte[] initResp = new byte[8];
                        initResp[0] = IdInitialization;
                        initResp[1] = 0x00;
                        BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(2, 2), seq);
                        BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(4, 2), version);
                        BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(6, 2), 2048);
                        server.Send(initResp, initResp.Length, remote);
                        initialized = true;
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    // 忽略超时，继续尝试
                }
            }

            return remote;
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_Initialize_Success()
        {
            await RunWithRetries(async () =>
            {
                int port = GetFreeUdpPort();
                using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                server.Client.ReceiveTimeout = 2000;
                var serverTask = Task.Run(() => { _ = CompleteHandshake(server); });
                using var transport = new UdpTransport("127.0.0.1", port, TestTimeoutMs, TestMaxAttempts);
                await serverTask;
                await Task.Delay(50); // 确保端口释放
            });
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_Initialize_Fail_InvalidVersion()
        {
            await RunWithRetries(async () =>
            {
                int port = GetFreeUdpPort();
                using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                server.Client.ReceiveTimeout = 2000;
                var serverTask = Task.Run(() => { _ = CompleteHandshake(server, version: 0); });
                var ex = Assert.Throws<Exception>(() => new UdpTransport("127.0.0.1", port, TestTimeoutMs, TestMaxAttempts));
                Assert.Contains("invalid protocol version", ex.Message);
                await serverTask;
                await Task.Delay(50);
            });
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_WriteThenRead_Success()
        {
            await RunWithRetries(async () =>
            {
                int port = GetFreeUdpPort();
                using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                server.Client.ReceiveTimeout = 2000;
                var serverTask = Task.Run(() =>
                {
                    IPEndPoint remote = CompleteHandshake(server);
                    byte[] writePacket = server.Receive(ref remote);
                    Assert.Equal(IdFastboot, writePacket[0]);
                    ushort seq = BinaryPrimitives.ReadUInt16BigEndian(writePacket.AsSpan(2, 2));
                    string payload = Encoding.ASCII.GetString(writePacket, 4, writePacket.Length - 4);
                    Assert.Equal("foo", payload);
                    byte[] writeResp = new byte[4];
                    writeResp[0] = IdFastboot;
                    writeResp[1] = 0x00;
                    BinaryPrimitives.WriteUInt16BigEndian(writeResp.AsSpan(2, 2), seq);
                    server.Send(writeResp, writeResp.Length, remote);
                    byte[] readPoll = server.Receive(ref remote);
                    Assert.Equal(IdFastboot, readPoll[0]);
                    Assert.Equal(4, readPoll.Length);
                    ushort readSeq = BinaryPrimitives.ReadUInt16BigEndian(readPoll.AsSpan(2, 2));
                    byte[] readData = Encoding.ASCII.GetBytes("bar");
                    byte[] readResp = new byte[4 + readData.Length];
                    readResp[0] = IdFastboot;
                    readResp[1] = 0x00;
                    BinaryPrimitives.WriteUInt16BigEndian(readResp.AsSpan(2, 2), readSeq);
                    Array.Copy(readData, 0, readResp, 4, readData.Length);
                    server.Send(readResp, readResp.Length, remote);
                });
                using var transport = new UdpTransport("127.0.0.1", port, TestTimeoutMs, TestMaxAttempts);
                long written = transport.Write(Encoding.ASCII.GetBytes("foo"), 3);
                Assert.Equal(3, written);
                byte[] read = transport.Read(3);
                Assert.Equal("bar", Encoding.ASCII.GetString(read));
                await serverTask;
                await Task.Delay(50);
            });
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_Read_Continuation_Success()
        {
            await RunWithRetries(async () =>
            {
                int port = GetFreeUdpPort();
                using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                server.Client.ReceiveTimeout = 2000;
                var serverTask = Task.Run(() =>
                {
                    IPEndPoint remote = CompleteHandshake(server);
                    byte[] readPoll1 = server.Receive(ref remote);
                    Assert.Equal(IdFastboot, readPoll1[0]);
                    Assert.Equal(4, readPoll1.Length);
                    ushort seq1 = BinaryPrimitives.ReadUInt16BigEndian(readPoll1.AsSpan(2, 2));
                    byte[] part1 = Encoding.ASCII.GetBytes("bar");
                    byte[] resp1 = new byte[4 + part1.Length];
                    resp1[0] = IdFastboot;
                    resp1[1] = 0x01; // continuation
                    BinaryPrimitives.WriteUInt16BigEndian(resp1.AsSpan(2, 2), seq1);
                    Array.Copy(part1, 0, resp1, 4, part1.Length);
                    server.Send(resp1, resp1.Length, remote);
                    byte[] readPoll2 = server.Receive(ref remote);
                    Assert.Equal(IdFastboot, readPoll2[0]);
                    Assert.Equal(4, readPoll2.Length);
                    ushort seq2 = BinaryPrimitives.ReadUInt16BigEndian(readPoll2.AsSpan(2, 2));
                    Assert.Equal((ushort)((seq1 + 1) & 0xFFFF), seq2);
                    byte[] part2 = Encoding.ASCII.GetBytes("baz");
                    byte[] resp2 = new byte[4 + part2.Length];
                    resp2[0] = IdFastboot;
                    resp2[1] = 0x00;
                    BinaryPrimitives.WriteUInt16BigEndian(resp2.AsSpan(2, 2), seq2);
                    Array.Copy(part2, 0, resp2, 4, part2.Length);
                    server.Send(resp2, resp2.Length, remote);
                });
                using var transport = new UdpTransport("127.0.0.1", port, TestTimeoutMs, TestMaxAttempts);
                byte[] read = transport.Read(6);
                Assert.Equal("barbaz", Encoding.ASCII.GetString(read));
                await serverTask;
                await Task.Delay(50);
            });
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_Write_OutOfTurnData_Fails()
        {
            await RunWithRetries(async () =>
            {
                int port = GetFreeUdpPort();
                using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                server.Client.ReceiveTimeout = 2000;
                var serverTask = Task.Run(() =>
                {
                    IPEndPoint remote = CompleteHandshake(server);
                    byte[] writePacket = server.Receive(ref remote);
                    ushort seq = BinaryPrimitives.ReadUInt16BigEndian(writePacket.AsSpan(2, 2));
                    byte[] unexpectedData = Encoding.ASCII.GetBytes("bar");
                    byte[] writeResp = new byte[4 + unexpectedData.Length];
                    writeResp[0] = IdFastboot;
                    writeResp[1] = 0x00;
                    BinaryPrimitives.WriteUInt16BigEndian(writeResp.AsSpan(2, 2), seq);
                    Array.Copy(unexpectedData, 0, writeResp, 4, unexpectedData.Length);
                    server.Send(writeResp, writeResp.Length, remote);
                });
                using var transport = new UdpTransport("127.0.0.1", port, TestTimeoutMs, TestMaxAttempts);
                await Assert.ThrowsAsync<Exception>(() => Task.Run(() => transport.Write(Encoding.ASCII.GetBytes("foo"), 3)));
                await serverTask;
                await Task.Delay(50);
            });
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_Write_ErrorResponse_Fails()
        {
            await RunWithRetries(async () =>
            {
                int port = GetFreeUdpPort();
                using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                server.Client.ReceiveTimeout = 2000;
                var serverTask = Task.Run(() =>
                {
                    IPEndPoint remote = CompleteHandshake(server);
                    byte[] writePacket = server.Receive(ref remote);
                    ushort seq = BinaryPrimitives.ReadUInt16BigEndian(writePacket.AsSpan(2, 2));
                    byte[] err = Encoding.ASCII.GetBytes("E");
                    byte[] errResp = new byte[4 + err.Length];
                    errResp[0] = IdError;
                    errResp[1] = 0x00;
                    BinaryPrimitives.WriteUInt16BigEndian(errResp.AsSpan(2, 2), seq);
                    Array.Copy(err, 0, errResp, 4, err.Length);
                    server.Send(errResp, errResp.Length, remote);
                });
                using var transport = new UdpTransport("127.0.0.1", port, TestTimeoutMs, TestMaxAttempts);
                await Assert.ThrowsAsync<Exception>(async () => await Task.Run(() => transport.Write(Encoding.ASCII.GetBytes("foo"), 3)));
                await serverTask;
                await Task.Delay(50);
            });
        }

        // 通用重试包装器，提升测试健壮性
        private static async Task RunWithRetries(Func<Task> testFunc, int maxRetries = 3)
        {
            Exception? last = null;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await testFunc();
                    return;
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    last = ex;
                    await Task.Delay(100);
                }
            }
            throw last ?? new Exception("Test failed after retries");
        }
    }
}

