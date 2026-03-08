using FirmwareKit.Comm.Fastboot.Usb;
using FirmwareKit.Sparse.Core;
using System.Globalization;
using System.Text;

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

        private sealed class RetryAwareDownloadCaptureTransport : IFastbootTransport
        {
            private readonly Queue<byte[]> _readQueue = new();
            private int _pendingDownloadBytes;
            private bool _failedFirstPayloadWrite;

            public MemoryStream DownloadPayload { get; } = new();

            public RetryAwareDownloadCaptureTransport(bool failFirstPayloadWrite)
            {
                _failedFirstPayloadWrite = failFirstPayloadWrite;
            }

            public byte[] Read(int length)
            {
                if (_readQueue.Count == 0)
                {
                    return Array.Empty<byte>();
                }
                return _readQueue.Dequeue();
            }

            public long Write(byte[] data, int length)
            {
                string command = Encoding.UTF8.GetString(data, 0, length);
                if (command.Equals("getvar:has-crc", StringComparison.OrdinalIgnoreCase))
                {
                    _readQueue.Enqueue(Encoding.UTF8.GetBytes("OKAYno"));
                    return length;
                }

                if (command.StartsWith("download:", StringComparison.OrdinalIgnoreCase))
                {
                    string hex = command.Substring("download:".Length);
                    int size = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    _pendingDownloadBytes = size;
                    _readQueue.Enqueue(Encoding.UTF8.GetBytes($"DATA{size:x8}"));
                    return length;
                }

                if (_pendingDownloadBytes > 0)
                {
                    if (_failedFirstPayloadWrite)
                    {
                        _failedFirstPayloadWrite = false;
                        return Math.Max(0, length - 1);
                    }

                    DownloadPayload.Write(data, 0, length);
                    _pendingDownloadBytes -= length;
                    if (_pendingDownloadBytes <= 0)
                    {
                        _readQueue.Enqueue(Encoding.UTF8.GetBytes("OKAY"));
                    }
                    return length;
                }

                _readQueue.Enqueue(Encoding.UTF8.GetBytes("OKAY"));
                return length;
            }

            public void Dispose() { }
        }

        private sealed class NonSeekableStream : Stream
        {
            private readonly Stream _inner;

            public NonSeekableStream(Stream inner)
            {
                _inner = inner;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
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
            transport.EnqueueResponse("OKAYno");
            for (int i = 0; i < 4; i++)
            {
                transport.EnqueueResponse("DATA00000010");
            }
            var util = new FastbootDriver(transport);

            using var shortStream = new MemoryStream(new byte[4]);
            var response = util.DownloadData(shortStream, 16);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Contains("Max retries exceeded", response.Response);
            Assert.Contains("stream ended early", response.Response);
        }

        [Fact]
        public void DownloadDataStream_DataSizeMismatch_Fails()
        {
            var transport = new MockTransport();
            transport.EnqueueResponse("OKAYno");
            transport.EnqueueResponse("DATA00000004");
            var util = new FastbootDriver(transport);

            using var stream = new MemoryStream(new byte[8]);
            var response = util.DownloadData(stream, 8);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Contains("download size mismatch", response.Response);
            Assert.DoesNotContain("Short write", response.Response);
        }

        [Fact]
        public void DownloadDataStream_Retry_UsesInitialStreamPosition()
        {
            var transport = new RetryAwareDownloadCaptureTransport(failFirstPayloadWrite: true);
            var util = new FastbootDriver(transport);

            byte[] source = new byte[] { 0xAA, 0xBB, 0x01, 0x02, 0x03, 0x04 };
            using var stream = new MemoryStream(source);
            stream.Position = 2;

            var response = util.DownloadData(stream, 4);

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, transport.DownloadPayload.ToArray());
        }

        [Fact]
        public void DownloadDataStream_NonSeekable_DoesNotRetry()
        {
            var transport = new RetryAwareDownloadCaptureTransport(failFirstPayloadWrite: true);
            var util = new FastbootDriver(transport);

            using var baseStream = new MemoryStream(new byte[] { 0x01, 0x02, 0x03, 0x04 });
            using var nonSeek = new NonSeekableStream(baseStream);

            var response = util.DownloadData(nonSeek, 4);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Contains("non-seekable stream", response.Response);
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
        public void DownloadDataBytes_DataSizeMismatch_Fails()
        {
            var transport = new MockTransport();
            transport.EnqueueResponse("DATA00000004");
            var util = new FastbootDriver(transport);

            var response = util.DownloadData(new byte[8]);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Contains("download size mismatch", response.Response);
        }

        [Fact]
        public void DownloadDataStream_MalformedCrcResponse_Fails()
        {
            var transport = new MockTransport();
            transport.EnqueueResponse("OKAYyes");
            transport.EnqueueResponse("DATA00000004");
            transport.EnqueueResponse("OKAY0xGGGGGGGG");
            var util = new FastbootDriver(transport);

            using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var response = util.DownloadData(stream, 4);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Contains("invalid CRC response", response.Response);
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

        [Fact]
        public void FlashUnsparseImage_OversizedRaw_IsConvertedToSparseAndFlashed()
        {
            byte[] imageBytes = new byte[128];
            var transport = new ProtocolDownloadCaptureTransport(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["getvar:has-slot:boot"] = "OKAYno",
                ["getvar:is-logical:boot"] = "OKAYno",
                ["getvar:max-download-size"] = "OKAY0x40",
                ["flash:boot"] = "OKAY"
            });

            var util = new FastbootDriver(transport);

            using var stream = new MemoryStream(imageBytes);
            var response = util.FlashUnsparseImage("boot", stream, stream.Length);

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Contains(transport.Commands, c => c.StartsWith("download:", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("flash:boot", transport.Commands);
        }

        [Fact]
        public void FlashUnsparseImage_LogicalPartition_ResizesBeforeFlash()
        {
            byte[] imageBytes = new byte[64];
            var transport = new ProtocolDownloadCaptureTransport(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["getvar:is-logical:system_b"] = "OKAYyes",
                ["getvar:is-userspace"] = "OKAYyes",
                ["resize-logical-partition:system_b:64"] = "OKAY",
                ["flash:system_b"] = "OKAY"
            });

            var util = new FastbootDriver(transport);
            using var stream = new MemoryStream(imageBytes);

            var response = util.FlashUnsparseImage("system_b", stream, stream.Length);

            Assert.Equal(FastbootState.Success, response.Result);
            int resizeIndex = transport.Commands.FindIndex(c => c.Equals("resize-logical-partition:system_b:64", StringComparison.OrdinalIgnoreCase));
            int downloadIndex = transport.Commands.FindIndex(c => c.StartsWith("download:", StringComparison.OrdinalIgnoreCase));
            Assert.True(resizeIndex >= 0);
            Assert.True(downloadIndex > resizeIndex);
            Assert.Contains("flash:system_b", transport.Commands);
        }

        [Fact]
        public void FlashSparseFile_TinyLimit_FallsBackToSingleSparseTransfer()
        {
            var transport = new ProtocolDownloadCaptureTransport(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["flash:boot"] = "OKAY"
            });
            var util = new FastbootDriver(transport);

            using var sparse = new SparseFile(4096, 4096);
            sparse.AddRawChunk(new byte[4096]);

            var response = util.FlashSparseFile("boot", sparse, 64);

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Contains(transport.Commands, c => c.StartsWith("download:", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("flash:boot", transport.Commands);
        }
    }
}
