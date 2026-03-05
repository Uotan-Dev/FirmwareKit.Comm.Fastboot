using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FirmwareKit.Comm.Fastboot.Backend.Network;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class TcpTransportTests
    {
        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Fact]
        public async Task TcpConnect_Success_FB01()
        {
            int port = GetFreePort();
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var serverTask = Task.Run(() =>
            {
                using var client = listener.AcceptTcpClient();
                using var stream = client.GetStream();
                byte[] handshake = new byte[4];
                stream.ReadExactly(handshake, 0, 4);
                Assert.Equal("FB01", Encoding.ASCII.GetString(handshake));
                stream.Write(Encoding.ASCII.GetBytes("FB01"));
            });

            using var transport = new TcpTransport("127.0.0.1", port);
            await serverTask;
        }

        [Fact]
        public async Task TcpConnect_Success_NewerVersion()
        {
            int port = GetFreePort();
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var serverTask = Task.Run(() =>
            {
                using var client = listener.AcceptTcpClient();
                using var stream = client.GetStream();
                byte[] handshake = new byte[4];
                stream.ReadExactly(handshake, 0, 4);
                Assert.Equal("FB01", Encoding.ASCII.GetString(handshake));
                stream.Write(Encoding.ASCII.GetBytes("FB99"));
            });

            using var transport = new TcpTransport("127.0.0.1", port);
            await serverTask;
        }

        [Fact]
        public async Task TcpConnect_Fail_BadHeader()
        {
            int port = GetFreePort();
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var serverTask = Task.Run(() =>
            {
                using var client = listener.AcceptTcpClient();
                using var stream = client.GetStream();
                byte[] handshake = new byte[4];
                stream.ReadExactly(handshake, 0, 4);
                stream.Write(Encoding.ASCII.GetBytes("XX01"));
            });

            var ex = Assert.Throws<Exception>(() => new TcpTransport("127.0.0.1", port));
            Assert.Contains("unrecognized initialization message", ex.Message);
            await serverTask;
        }

        [Fact]
        public async Task TcpConnect_Fail_UnsupportedVersion()
        {
            int port = GetFreePort();
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var serverTask = Task.Run(() =>
            {
                using var client = listener.AcceptTcpClient();
                using var stream = client.GetStream();
                byte[] handshake = new byte[4];
                stream.ReadExactly(handshake, 0, 4);
                stream.Write(Encoding.ASCII.GetBytes("FB00"));
            });

            var ex = Assert.Throws<Exception>(() => new TcpTransport("127.0.0.1", port));
            Assert.Contains("unknown TCP protocol version 00", ex.Message);
            await serverTask;
        }

        [Fact]
        public async Task Tcp_WriteAndRead_MessageFrame_Success()
        {
            int port = GetFreePort();
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var serverTask = Task.Run(() =>
            {
                using var client = listener.AcceptTcpClient();
                using var stream = client.GetStream();

                var hs = new byte[4];
                stream.ReadExactly(hs, 0, 4);
                stream.Write(Encoding.ASCII.GetBytes("FB01"));

                byte[] lenBuffer = new byte[8];
                stream.ReadExactly(lenBuffer, 0, 8);
                long len = BinaryPrimitives.ReadInt64BigEndian(lenBuffer);
                Assert.Equal(3, len);

                byte[] payload = new byte[3];
                stream.ReadExactly(payload, 0, 3);
                Assert.Equal("foo", Encoding.ASCII.GetString(payload));

                byte[] outLen = new byte[8];
                BinaryPrimitives.WriteInt64BigEndian(outLen, 3);
                stream.Write(outLen);
                stream.Write(Encoding.ASCII.GetBytes("bar"));
            });

            using var transport = new TcpTransport("127.0.0.1", port);
            long written = transport.Write(Encoding.ASCII.GetBytes("foo"), 3);
            Assert.Equal(3, written);

            byte[] read = transport.Read(3);
            Assert.Equal("bar", Encoding.ASCII.GetString(read));

            await serverTask;
        }

        [Fact]
        public async Task Tcp_Read_FragmentedFrame_Success()
        {
            int port = GetFreePort();
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var serverTask = Task.Run(() =>
            {
                using var client = listener.AcceptTcpClient();
                using var stream = client.GetStream();

                byte[] hs = new byte[4];
                stream.ReadExactly(hs, 0, 4);
                stream.Write(Encoding.ASCII.GetBytes("FB01"));

                byte[] lenBuffer = new byte[8];
                BinaryPrimitives.WriteInt64BigEndian(lenBuffer, 3);
                stream.Write(lenBuffer, 0, 4);
                stream.Write(lenBuffer, 4, 4);
                stream.Write(Encoding.ASCII.GetBytes("f"));
                stream.Write(Encoding.ASCII.GetBytes("o"));
                stream.Write(Encoding.ASCII.GetBytes("o"));
            });

            using var transport = new TcpTransport("127.0.0.1", port);
            byte[] read = transport.Read(3);
            Assert.Equal("foo", Encoding.ASCII.GetString(read));

            await serverTask;
        }
    }
}
