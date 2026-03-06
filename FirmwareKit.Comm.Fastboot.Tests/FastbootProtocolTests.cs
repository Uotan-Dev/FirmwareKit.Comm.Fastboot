using System.Globalization;
using System.Text;
using FirmwareKit.Comm.Fastboot.Usb;
using FirmwareKit.Comm.Fastboot;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class FastbootProtocolTests
    {
        private class MockTransport : IFastbootTransport
        {
            private readonly Queue<byte[]> _responses = new Queue<byte[]>();
            private readonly Queue<long> _writeResults = new Queue<long>();
            public List<string> Commands { get; } = new List<string>();

            public void EnqueueResponse(string response)
            {
                _responses.Enqueue(Encoding.UTF8.GetBytes(response));
            }

            public byte[] Read(int length)
            {
                if (_responses.Count == 0) return Array.Empty<byte>();
                return _responses.Dequeue();
            }

            public void EnqueueWriteResult(long result)
            {
                _writeResults.Enqueue(result);
            }

            public long Write(byte[] data, int length)
            {
                Commands.Add(Encoding.UTF8.GetString(data, 0, length));
                if (_writeResults.Count > 0) return _writeResults.Dequeue();
                return length;
            }

            public void Dispose() { }
        }

        private class MockUsbDevice : UsbDevice
        {
            private Queue<byte[]> _responses = new Queue<byte[]>();

            public MockUsbDevice()
            {
                DevicePath = "mock";
            }

            public void EnqueueResponse(string response)
            {
                _responses.Enqueue(Encoding.UTF8.GetBytes(response));
            }

            public override byte[] Read(int length)
            {
                if (_responses.Count == 0) return Array.Empty<byte>();
                return _responses.Dequeue();
            }

            public override long Write(byte[] data, int length) => length;
            public override int GetSerialNumber() => 0;
            public override int CreateHandle() => 0;
            public override void Reset() { }
            public override void Dispose() { }
        }

        private sealed class ProtocolDownloadCaptureTransport : IFastbootTransport
        {
            private readonly Dictionary<string, string> _responses;
            private readonly Queue<byte[]> _readQueue = new();
            private int _pendingDownloadBytes;

            public List<string> Commands { get; } = new();
            public MemoryStream DownloadPayload { get; } = new();

            public ProtocolDownloadCaptureTransport(Dictionary<string, string>? responses = null)
            {
                _responses = responses ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            public byte[] Read(int length)
            {
                if (_readQueue.Count == 0)
                {
                    return Encoding.UTF8.GetBytes("OKAY");
                }

                return _readQueue.Dequeue();
            }

            public long Write(byte[] data, int length)
            {
                if (_pendingDownloadBytes > 0)
                {
                    DownloadPayload.Write(data, 0, length);
                    _pendingDownloadBytes -= length;
                    if (_pendingDownloadBytes <= 0)
                    {
                        _readQueue.Enqueue(Encoding.UTF8.GetBytes("OKAY"));
                    }

                    return length;
                }

                string command = Encoding.UTF8.GetString(data, 0, length);
                Commands.Add(command);

                if (command.StartsWith("download:", StringComparison.OrdinalIgnoreCase))
                {
                    string hex = command.Substring("download:".Length);
                    int size = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    _pendingDownloadBytes = size;
                    _readQueue.Enqueue(Encoding.UTF8.GetBytes($"DATA{size:x8}"));
                    return length;
                }

                if (_responses.TryGetValue(command, out string? response))
                {
                    _readQueue.Enqueue(Encoding.UTF8.GetBytes(response));
                    return length;
                }

                _readQueue.Enqueue(Encoding.UTF8.GetBytes("OKAY"));
                return length;
            }

            public void Dispose() { }
        }

        [Fact]
        public void HandleResponse_Success_ReturnsOkay()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("OKAYDONE");
            var util = new FastbootDriver(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Equal("DONE", response.Response);
        }

        [Fact]
        public void HandleResponse_Fail_ReturnsFailure()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("FAILERROR_MESSAGE");
            var util = new FastbootDriver(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Equal("ERROR_MESSAGE", response.Response);
        }

        [Fact]
        public void HandleResponse_InfoThenOkay_CollectsInfo()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("INFOPartial Info 1");
            mockUsb.EnqueueResponse("INFOPartial Info 2");
            mockUsb.EnqueueResponse("OKAY");
            var util = new FastbootDriver(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Equal(2, response.Info.Count);
            Assert.Equal("Partial Info 1", response.Info[0]);
            Assert.Equal("Partial Info 2", response.Info[1]);
        }

        [Fact]
        public void HandleResponse_PackedInfoFramesThenOkay_Succeeds()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("INFOline1\0INFOline2\0OKAYDONE");
            var util = new FastbootDriver(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Equal("DONE", response.Response);
            Assert.Equal(2, response.Info.Count);
            Assert.Equal("line1", response.Info[0]);
            Assert.Equal("line2", response.Info[1]);
        }

        [Fact]
        public void HandleResponse_PackedTextFramesThenOkay_Succeeds()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("TEXTpart1\0TEXTpart2\0OKAY");
            var util = new FastbootDriver(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Equal("part1part2", response.Text);
        }

        [Fact]
        public void HandleResponse_LargeData_ReturnsDataAndSize()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("DATA80000000"); // 2GB in hex
            var util = new FastbootDriver(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Data, response.Result);
            Assert.Equal(2147483648L, response.DataSize);
        }

        [Fact]
        public void HandleResponse_MalformedDataField_Fails()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("DATA123456789"); // 9 hex chars should be rejected
            var util = new FastbootDriver(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Contains("data size malformed", response.Response);
        }

        [Fact]
        public void HandleResponse_DataFieldWithNonHex_Fails()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("DATA00000G10");
            var util = new FastbootDriver(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Contains("data size malformed", response.Response);
        }

        [Fact]
        public void HandleResponse_DataFieldMaxUInt32_Succeeds()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("DATAffffffff");
            var util = new FastbootDriver(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Data, response.Result);
            Assert.Equal(4294967295L, response.DataSize);
        }

        [Fact]
        public void HandleResponse_FragmentedDataPrefix_Succeeds()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("DA");
            mockUsb.EnqueueResponse("TA00000010");
            var util = new FastbootDriver(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Data, response.Result);
            Assert.Equal(16L, response.DataSize);
        }

        [Fact]
        public void DownloadDataStream_PrematureEof_FailsEarly()
        {
            var transport = new MockTransport();
            transport.EnqueueResponse("DATA00000010");
            var util = new FastbootDriver(transport);

            using var shortStream = new MemoryStream(new byte[4]);
            var response = util.DownloadData(shortStream, 16);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Contains("stream ended early", response.Response);
        }

        [Fact]
        public void DownloadDataBytes_ZeroLength_Fails()
        {
            var transport = new MockTransport();
            var util = new FastbootDriver(transport);

            var response = util.DownloadData(Array.Empty<byte>());

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Equal("invalid download size", response.Response);
        }

        [Fact]
        public void DownloadDataBytes_Uses8DigitLowerHexCommand()
        {
            var transport = new MockTransport();
            transport.EnqueueResponse("DATA00000010");
            transport.EnqueueResponse("OKAY");
            var util = new FastbootDriver(transport);

            var response = util.DownloadData(new byte[16]);

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.NotEmpty(transport.Commands);
            Assert.Equal("download:00000010", transport.Commands[0]);
        }

        [Fact]
        public void DownloadDataBytes_ShortWrite_Fails()
        {
            var transport = new MockTransport();
            transport.EnqueueResponse("DATA00000008");
            transport.EnqueueWriteResult("download:00000008".Length);
            transport.EnqueueWriteResult(4);
            var util = new FastbootDriver(transport);

            var response = util.DownloadData(new byte[8]);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Contains("Short write", response.Response);
        }

        [Fact]
        public void UploadData_SegmentedRead_Succeeds()
        {
            var transport = new MockTransport();
            transport.EnqueueResponse("DATA00000006");
            transport.EnqueueResponse("abc");
            transport.EnqueueResponse("def");
            transport.EnqueueResponse("OKAYDONE");
            var util = new FastbootDriver(transport);

            using var output = new MemoryStream();
            var response = util.UploadData("upload", output);

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Equal("DONE", response.Response);
            Assert.Equal("abcdef", Encoding.UTF8.GetString(output.ToArray()));
            Assert.NotEmpty(transport.Commands);
            Assert.Equal("upload", transport.Commands[0]);
        }

        [Fact]
        public void UploadData_EofDuringPayload_Throws()
        {
            var transport = new MockTransport();
            transport.EnqueueResponse("DATA00000006");
            transport.EnqueueResponse("abc");
            transport.EnqueueResponse("");
            var util = new FastbootDriver(transport);

            using var output = new MemoryStream();
            var ex = Assert.Throws<Exception>(() => util.UploadData("upload", output));

            Assert.Contains("Unexpected EOF", ex.Message);
        }

        [Fact]
        public void UploadData_FinalFail_IsReturned()
        {
            var transport = new MockTransport();
            transport.EnqueueResponse("DATA00000003");
            transport.EnqueueResponse("abc");
            transport.EnqueueResponse("FAILnope");
            var util = new FastbootDriver(transport);

            using var output = new MemoryStream();
            var response = util.UploadData("upload", output);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Equal("nope", response.Response);
            Assert.Equal("abc", Encoding.UTF8.GetString(output.ToArray()));
        }

        [Fact]
        public void FlashImage_Stream_NonSparse_RewindsBeforeDownload()
        {
            byte[] imageBytes = new byte[64];
            for (int i = 0; i < imageBytes.Length; i++) imageBytes[i] = (byte)i;

            var transport = new ProtocolDownloadCaptureTransport(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["getvar:has-slot:boot"] = "OKAYno",
                ["getvar:is-logical:boot"] = "OKAYno",
                ["getvar:max-download-size"] = "OKAY0x100000",
                ["flash:boot"] = "OKAY"
            });

            var util = new FastbootDriver(transport);

            using var stream = new MemoryStream(imageBytes);
            util.FlashImage("boot", stream);

            Assert.Contains("download:00000040", transport.Commands);
            Assert.Contains("flash:boot", transport.Commands);
            Assert.Equal(imageBytes, transport.DownloadPayload.ToArray());
        }
    }
}
