using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class UdpTransportTests
    {
        private const byte IdError = 0x00;
        private const byte IdDeviceQuery = 0x01;
        private const byte IdInitialization = 0x02;
        private const byte IdFastboot = 0x03;

        private static int GetFreeUdpPort()
        {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        }

        [Fact]
        public async Task Udp_Initialize_Success()
        {
            int port = GetFreeUdpPort();
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));

            var serverTask = Task.Run(() =>
            {
                IPEndPoint remote = new(IPAddress.Loopback, 0);

                byte[] q = server.Receive(ref remote);
                Assert.Equal(IdDeviceQuery, q[0]);
                Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(q.AsSpan(2, 2)));

                byte[] qResp = new byte[] { IdDeviceQuery, 0x00, 0x00, 0x00, 0x00, 0x00 };
                server.Send(qResp, qResp.Length, remote);

                byte[] init = server.Receive(ref remote);
                Assert.Equal(IdInitialization, init[0]);
                Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(init.AsSpan(2, 2)));

                byte[] initResp = new byte[8];
                initResp[0] = IdInitialization;
                initResp[1] = 0x00;
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(2, 2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(4, 2), 1);
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(6, 2), 2048);
                server.Send(initResp, initResp.Length, remote);
            });

            using var transport = new UdpTransport("127.0.0.1", port);
            await serverTask;
        }

        [Fact]
        public async Task Udp_Initialize_Fail_InvalidVersion()
        {
            int port = GetFreeUdpPort();
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));

            var serverTask = Task.Run(() =>
            {
                IPEndPoint remote = new(IPAddress.Loopback, 0);
                _ = server.Receive(ref remote);
                byte[] qResp = new byte[] { IdDeviceQuery, 0x00, 0x00, 0x00, 0x00, 0x00 };
                server.Send(qResp, qResp.Length, remote);

                _ = server.Receive(ref remote);
                byte[] initResp = new byte[8];
                initResp[0] = IdInitialization;
                initResp[1] = 0x00;
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(2, 2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(4, 2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(6, 2), 2048);
                server.Send(initResp, initResp.Length, remote);
            });

            var ex = Assert.Throws<Exception>(() => new UdpTransport("127.0.0.1", port));
            Assert.Contains("invalid protocol version", ex.Message);
            await serverTask;
        }

        [Fact]
        public async Task Udp_WriteThenRead_Success()
        {
            int port = GetFreeUdpPort();
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));

            var serverTask = Task.Run(() =>
            {
                IPEndPoint remote = new(IPAddress.Loopback, 0);

                _ = server.Receive(ref remote);
                byte[] qResp = new byte[] { IdDeviceQuery, 0x00, 0x00, 0x00, 0x00, 0x00 };
                server.Send(qResp, qResp.Length, remote);

                _ = server.Receive(ref remote);
                byte[] initResp = new byte[8];
                initResp[0] = IdInitialization;
                initResp[1] = 0x00;
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(2, 2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(4, 2), 1);
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(6, 2), 2048);
                server.Send(initResp, initResp.Length, remote);

                byte[] writePacket = server.Receive(ref remote);
                Assert.Equal(IdFastboot, writePacket[0]);
                ushort seq = BinaryPrimitives.ReadUInt16BigEndian(writePacket.AsSpan(2, 2));
                string payload = Encoding.ASCII.GetString(writePacket, 4, writePacket.Length - 4);
                Assert.Equal("foo", payload);

                byte[] ackWithData = Encoding.ASCII.GetBytes("bar");
                byte[] writeResp = new byte[4 + ackWithData.Length];
                writeResp[0] = IdFastboot;
                writeResp[1] = 0x00;
                BinaryPrimitives.WriteUInt16BigEndian(writeResp.AsSpan(2, 2), seq);
                Array.Copy(ackWithData, 0, writeResp, 4, ackWithData.Length);
                server.Send(writeResp, writeResp.Length, remote);
            });

            using var transport = new UdpTransport("127.0.0.1", port);
            long written = transport.Write(Encoding.ASCII.GetBytes("foo"), 3);
            Assert.Equal(3, written);

            byte[] read = transport.Read(3);
            Assert.Equal("bar", Encoding.ASCII.GetString(read));

            await serverTask;
        }

        [Fact]
        public async Task Udp_Write_ErrorResponse_Fails()
        {
            int port = GetFreeUdpPort();
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));

            var serverTask = Task.Run(() =>
            {
                IPEndPoint remote = new(IPAddress.Loopback, 0);

                _ = server.Receive(ref remote);
                byte[] qResp = new byte[] { IdDeviceQuery, 0x00, 0x00, 0x00, 0x00, 0x00 };
                server.Send(qResp, qResp.Length, remote);

                _ = server.Receive(ref remote);
                byte[] initResp = new byte[8];
                initResp[0] = IdInitialization;
                initResp[1] = 0x00;
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(2, 2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(4, 2), 1);
                BinaryPrimitives.WriteUInt16BigEndian(initResp.AsSpan(6, 2), 2048);
                server.Send(initResp, initResp.Length, remote);

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

            using var transport = new UdpTransport("127.0.0.1", port);
            var ex = Assert.Throws<Exception>(() => transport.Write(Encoding.ASCII.GetBytes("foo"), 3));
            Assert.Contains("error response", ex.Message);

            await serverTask;
        }
    }
}
