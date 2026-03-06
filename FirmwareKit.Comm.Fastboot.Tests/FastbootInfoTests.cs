using FirmwareKit.Comm.Fastboot.Backend.Network;
using System.Globalization;
using System.Text;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class FastbootInfoTests
    {
        private sealed class ScriptedTransport : IFastbootTransport
        {
            private string _lastCommand = string.Empty;

            public byte[] Read(int length)
            {
                if (_lastCommand.StartsWith("getvar:current-slot", StringComparison.OrdinalIgnoreCase))
                {
                    return Encoding.UTF8.GetBytes("OKAYa");
                }
                return Encoding.UTF8.GetBytes("OKAY");
            }

            public long Write(byte[] data, int length)
            {
                _lastCommand = Encoding.UTF8.GetString(data, 0, length);
                return length;
            }

            public void Dispose() { }
        }

        private sealed class ProtocolScriptTransport : IFastbootTransport
        {
            private readonly Dictionary<string, string> _responses;
            private readonly Queue<byte[]> _readQueue = new();
            private int _pendingDownloadBytes = 0;

            public List<string> Commands { get; } = new();

            public ProtocolScriptTransport(Dictionary<string, string>? responses = null)
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
        public void CheckFastbootInfoRequirements_CorrectVersions_ReturnsTrue()
        {
            var util = new FastbootUtil(new ScriptedTransport());
            string[] correctVersions = { "1", "2" };

            foreach (string version in correctVersions)
            {
                Assert.True(util.CheckFastbootInfoRequirements(version));
            }
        }

        [Fact]
        public void CheckFastbootInfoRequirements_BadVersions_ReturnsFalse()
        {
            var util = new FastbootUtil(new ScriptedTransport());
            string[] badVersions = { "", ".01", "x1", "1.0.1", "1.", "1.0 2.0", "100.00", "3", "17", "22" };

            foreach (string version in badVersions)
            {
                Assert.False(util.CheckFastbootInfoRequirements(version));
            }
        }

        [Fact]
        public void FlashFromInfo_UnknownCommand_Throws()
        {
            var util = new FastbootUtil(new ScriptedTransport());
            string info = "version 1\nunknown-cmd";

            var ex = Assert.Throws<InvalidDataException>(() =>
                util.FlashFromInfo(info, Path.GetTempPath(), wipe: false, slotOverride: "a", optimizeSuper: false));

            Assert.Contains("Unknown command in fastboot-info.txt", ex.Message);
        }

        [Fact]
        public void FlashFromInfo_IfWipeFalse_SkipsEraseTask()
        {
            var transport = new ProtocolScriptTransport(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["getvar:has-slot:userdata"] = "OKAYno"
            });
            var util = new FastbootUtil(transport);

            string info = "version 1\nif-wipe erase userdata";
            util.FlashFromInfo(info, Path.GetTempPath(), wipe: false, slotOverride: "a", optimizeSuper: false);

            Assert.DoesNotContain("erase:userdata", transport.Commands);
        }

        [Fact]
        public void FlashFromInfo_IfWipeTrue_ExecutesEraseTask()
        {
            var transport = new ProtocolScriptTransport(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["getvar:has-slot:userdata"] = "OKAYno"
            });
            var util = new FastbootUtil(transport);

            string info = "version 1\nif-wipe erase userdata";
            util.FlashFromInfo(info, Path.GetTempPath(), wipe: true, slotOverride: "a", optimizeSuper: false);

            Assert.Contains("erase:userdata", transport.Commands);
        }

        [Fact]
        public void FlashFromInfo_SlotOther_FlashesToOtherSlot()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FastbootInfoTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string imagePath = Path.Combine(tempDir, "system_other.img");
                File.WriteAllBytes(imagePath, new byte[] { 0x00, 0x01, 0x02 });

                var transport = new ProtocolScriptTransport(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["getvar:has-slot:system"] = "OKAYyes",
                    ["getvar:is-logical:system_b"] = "OKAYno",
                    ["getvar:max-download-size"] = "OKAY0x100000",
                    ["flash:system_b"] = "OKAY"
                });
                var util = new FastbootUtil(transport);

                string info = "version 1\nflash --slot-other system system_other.img";
                util.FlashFromInfo(info, tempDir, wipe: false, slotOverride: "a", optimizeSuper: false);

                Assert.Contains("flash:system_b", transport.Commands);
                Assert.DoesNotContain("flash:system_a", transport.Commands);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FlashFromInfo_RebootCommands_MappedCorrectly()
        {
            var transport = new ProtocolScriptTransport();
            var util = new FastbootUtil(transport);

            string info = "version 1\nreboot bootloader\nreboot";
            util.FlashFromInfo(info, Path.GetTempPath(), wipe: false, slotOverride: "a", optimizeSuper: false);

            Assert.Contains("reboot-bootloader", transport.Commands);
            Assert.Contains("reboot", transport.Commands);
        }

        [Fact]
        public void GetMaxDownloadSize_ClampsToSparseLimit()
        {
            int originalLimit = FastbootUtil.SparseMaxDownloadSize;
            try
            {
                FastbootUtil.SparseMaxDownloadSize = 1024 * 1024 * 1024;
                var transport = new ProtocolScriptTransport(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["getvar:max-download-size"] = "OKAY0x80000000"
                });
                var util = new FastbootUtil(transport);

                long maxDownloadSize = util.GetMaxDownloadSize();

                Assert.Equal(FastbootUtil.SparseMaxDownloadSize, maxDownloadSize);
            }
            finally
            {
                FastbootUtil.SparseMaxDownloadSize = originalLimit;
            }
        }

        [Fact]
        public void GetVar_FailureIsNotCached_AllowsRetrySuccess()
        {
            // First read fails, second read succeeds for same key.
            int attempt = 0;
            string key = "max-download-size";

            var util = new FastbootUtil(new DelegatingTransport(
                onWrite: (cmd) =>
                {
                    if (cmd.Equals("getvar:" + key, StringComparison.OrdinalIgnoreCase))
                    {
                        attempt++;
                        return attempt == 1 ? "FAILtemporary" : "OKAY0x100000";
                    }
                    return "OKAY";
                }));

            string first = util.GetVar(key);
            string second = util.GetVar(key);

            Assert.Equal(string.Empty, first);
            Assert.Equal("0x100000", second);
        }

        private sealed class DelegatingTransport : IFastbootTransport
        {
            private readonly Func<string, string> _onWrite;
            private readonly Queue<byte[]> _readQueue = new();

            public DelegatingTransport(Func<string, string> onWrite)
            {
                _onWrite = onWrite;
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
                string cmd = Encoding.UTF8.GetString(data, 0, length);
                string response = _onWrite(cmd);
                _readQueue.Enqueue(Encoding.UTF8.GetBytes(response));
                return length;
            }

            public void Dispose() { }
        }
    }
}
