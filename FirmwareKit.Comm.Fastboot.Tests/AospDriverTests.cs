using System.Text;
using FirmwareKit.Comm.Fastboot;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class AospDriverTests
    {
        private class MockTransport : IFastbootTransport
        {
            private readonly Queue<byte[]> _responses = new Queue<byte[]>();
            public List<string> WrittenCommands { get; } = new List<string>();

            public void EnqueueResponse(string response)
            {
                _responses.Enqueue(Encoding.UTF8.GetBytes(response));
            }

            public byte[] Read(int length)
            {
                if (_responses.Count == 0) return Array.Empty<byte>();
                return _responses.Dequeue();
            }

            public long Write(byte[] data, int length)
            {
                string cmd = Encoding.UTF8.GetString(data, 0, length);
                WrittenCommands.Add(cmd);
                return length;
            }

            public string Host => "localhost";
            public int Port => 5554;
            public void Dispose() { }
        }

        [Fact]
        public void Test_Boot_Aosp()
        {
            // Ported from fastboot_driver_test.cpp: TEST_F(DriverTest, Boot)
            var transport = new MockTransport();
            var util = new FastbootDriver(transport);

            transport.EnqueueResponse("OKAY");

            var response = util.RawCommand("boot");

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Contains("boot", transport.WrittenCommands);
        }

        [Fact]
        public void Test_Continue_Aosp()
        {
            // Ported from fastboot_driver_test.cpp: TEST_F(DriverTest, Continue)
            var transport = new MockTransport();
            var util = new FastbootDriver(transport);

            transport.EnqueueResponse("OKAY");

            var response = util.Continue();

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Contains("continue", transport.WrittenCommands);
        }

        [Fact]
        public void Test_Erase_Aosp()
        {
            // Ported from fastboot_driver_test.cpp: TEST_F(DriverTest, Erase)
            var transport = new MockTransport();
            var util = new FastbootDriver(transport);

            transport.EnqueueResponse("OKAY");

            var response = util.ErasePartition("partition");

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Contains("erase:partition", transport.WrittenCommands);
        }

        [Fact]
        public void Test_Flash_Aosp()
        {
            // Ported from fastboot_driver_test.cpp: TEST_F(DriverTest, Flash)
            var transport = new MockTransport();
            var util = new FastbootDriver(transport);

            transport.EnqueueResponse("OKAY");

            var response = util.RawCommand("flash:partition");

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Contains("flash:partition", transport.WrittenCommands);
        }

        [Fact]
        public void Test_GetVarAll_Aosp()
        {
            // Ported from fastboot_driver_test.cpp: TEST_F(DriverTest, GetVarAll)
            var transport = new MockTransport();
            var util = new FastbootDriver(transport);

            transport.EnqueueResponse("INFOversion:0.4");
            transport.EnqueueResponse("INFOslot-count:2");
            transport.EnqueueResponse("OKAY");

            var vars = util.GetVarAll();

            Assert.Equal("0.4", vars["version"]);
            Assert.Equal("2", vars["slot-count"]);
            Assert.Contains("getvar:all", transport.WrittenCommands);
        }

        [Fact]
        public void Test_Reboot_Aosp()
        {
            // Ported from fastboot_driver_test.cpp: TEST_F(DriverTest, Reboot)
            var transport = new MockTransport();
            var util = new FastbootDriver(transport);

            transport.EnqueueResponse("OKAY");

            var response = util.Reboot("");

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Contains("reboot", transport.WrittenCommands);
        }
    }
}
