using FirmwareKit.Comm.Fastboot.DataModel;
using FirmwareKit.Comm.Fastboot.Backend.Network;
using System.Collections.Generic;
using System.Text;
using System;
using Xunit;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class AospFastbootDriverTests
    {
        private class MockTransport : IFastbootTransport
        {
            private readonly Queue<byte[]> _responses = new Queue<byte[]>();
            public List<string> WrittenCommands { get; } = new List<string>();

            public void EnqueueResponse(string response)
            {
                // AOSP fastboot protocol responses are truncated at 256 bytes in some cases,
                // but the string itself is what matters for the test.
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

            public void Dispose() { }
        }

        [Fact]
        public void Test_GetVar_Aosp()
        {
            // Ported from: fastboot_driver_test.cpp -> TEST_F(DriverTest, GetVar)
            var transport = new MockTransport();
            var util = new FastbootUtil(transport);

            transport.EnqueueResponse("OKAY0.4");

            string output = util.GetVar("version");

            Assert.Equal("0.4", output);
            Assert.Contains("getvar:version", transport.WrittenCommands);
        }

        [Fact]
        public void Test_InfoMessage_Aosp()
        {
            // Ported from: fastboot_driver_test.cpp -> TEST_F(DriverTest, InfoMessage)
            var transport = new MockTransport();
            var util = new FastbootUtil(transport);

            transport.EnqueueResponse("INFOthis is an info line");
            transport.EnqueueResponse("OKAY");

            var response = util.RawCommand("oem dmesg");

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Single(response.Info);
            Assert.Equal("this is an info line", response.Info[0]);
            Assert.Contains("oem dmesg", transport.WrittenCommands);
        }

        [Fact]
        public void Test_TextMessage_Aosp()
        {
            // Ported from: fastboot_driver_test.cpp -> TEST_F(DriverTest, TextMessage)
            var transport = new MockTransport();
            var util = new FastbootUtil(transport);
            string capturedText = "";

            util.ReceivedFromDevice += (s, e) =>
            {
                if (e.Type == FastbootState.Text)
                {
                    capturedText += e.NewText;
                }
            };

            transport.EnqueueResponse("TEXTthis is a text line");
            transport.EnqueueResponse("TEXT, albeit very long and split over multiple TEXT messages.");
            transport.EnqueueResponse("TEXT Indeed we can do that now with a TEXT message whenever we feel like it.");
            transport.EnqueueResponse("TEXT Isn't that truly super cool?");
            transport.EnqueueResponse("OKAY");

            var response = util.RawCommand("oem trusty runtest trusty.hwaes.bench");

            Assert.Equal(FastbootState.Success, response.Result);
            string expected = "this is a text line" +
                              ", albeit very long and split over multiple TEXT messages." +
                              " Indeed we can do that now with a TEXT message whenever we feel like it." +
                              " Isn't that truly super cool?";
            Assert.Equal(expected, capturedText);
            // Also check the accumulated text in response object
            Assert.Equal(expected, response.Text);
        }

        [Fact]
        public void Test_Download_Fail_Aosp()
        {
            // Logical check: If download command fails, should return FAIL immediately
            var transport = new MockTransport();
            var util = new FastbootUtil(transport);

            transport.EnqueueResponse("FAILdata too large");

            var response = util.DownloadData(new byte[1024]);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Equal("data too large", response.Response);
        }
    }
}
