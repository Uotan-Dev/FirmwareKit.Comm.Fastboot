using FirmwareKit.Comm.Fastboot.Network;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class UdpTransportTests
    {
        private const int TestTimeoutMs = 250;
        private const int TestMaxAttempts = 5;

        private const byte IdError = 0x00;
        private const byte IdDeviceQuery = 0x01;
        private const byte IdInitialization = 0x02;
        private const byte IdFastboot = 0x03;

        private static int GetFreeUdpPort()
        {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        }

        private static IPEndPoint CompleteHandshake(UdpClient server, ushort version = 1)
        {
            IPEndPoint remote = new(IPAddress.Loopback, 0);

            bool initialized = false;
            while (!initialized)
            {
                byte[] packet = server.Receive(ref remote);
                if (packet.Length < 4)
                {
                    continue;
                }

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

            return remote;
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_Initialize_Success()
        {
            int port = GetFreeUdpPort();
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            server.Client.ReceiveTimeout = 2000;

            var serverTask = Task.Run(() =>
            {
                _ = CompleteHandshake(server);
            });

            using var transport = new UdpTransport("127.0.0.1", port, TestTimeoutMs, TestMaxAttempts);
            await serverTask;
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_Initialize_Fail_InvalidVersion()
        {
            int port = GetFreeUdpPort();
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            server.Client.ReceiveTimeout = 2000;

            var serverTask = Task.Run(() =>
            {
                _ = CompleteHandshake(server, version: 0);
            });

            var ex = Assert.Throws<Exception>(() => new UdpTransport("127.0.0.1", port, TestTimeoutMs, TestMaxAttempts));
            Assert.Contains("invalid protocol version", ex.Message);
            await serverTask;
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_WriteThenRead_Success()
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

                // Write path should only receive an empty ACK.
                byte[] writeResp = new byte[4];
                writeResp[0] = IdFastboot;
                writeResp[1] = 0x00;
                BinaryPrimitives.WriteUInt16BigEndian(writeResp.AsSpan(2, 2), seq);
                server.Send(writeResp, writeResp.Length, remote);

                // Read path should actively poll by sending an empty fastboot packet.
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
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_Read_Continuation_Success()
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
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_Write_OutOfTurnData_Fails()
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
        }

        [Fact(Timeout = 5000)]
        public async Task Udp_Write_ErrorResponse_Fails()
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
        }
    }
}
