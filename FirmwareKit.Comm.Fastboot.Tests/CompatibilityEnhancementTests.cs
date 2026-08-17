using System.Text;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class RawCommandLimitTests
    {
        private class CapturingTransport : IFastbootTransport
        {
            public List<string> Commands { get; } = new();
            public byte[] Read(int length) => Encoding.UTF8.GetBytes("OKAY");
            public long Write(byte[] data, int length)
            {
                Commands.Add(Encoding.UTF8.GetString(data, 0, length));
                return length;
            }
            public void Dispose() { }
        }

        [Fact]
        public void RawCommand_WithinLimit_SendsCommand()
        {
            var transport = new CapturingTransport();
            var driver = new FastbootDriver(transport);

            string command = new string('x', 4096);
            var response = driver.RawCommand(command);

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Single(transport.Commands);
            Assert.Equal(command, transport.Commands[0]);
        }

        [Fact]
        public void RawCommand_OverLimit_ReturnsFailWithoutSending()
        {
            var transport = new CapturingTransport();
            var driver = new FastbootDriver(transport);

            string command = new string('x', 4097);
            var response = driver.RawCommand(command);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Contains("too long", response.Response);
            Assert.Empty(transport.Commands);
        }

        [Fact]
        public void RawCommand_Utf8OverLimit_ReturnsFail()
        {
            var transport = new CapturingTransport();
            var driver = new FastbootDriver(transport);

            // 3000 3-byte characters -> 9000 UTF-8 bytes > 4096.
            string command = new string('\u4E2D', 3000);
            var response = driver.RawCommand(command);

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Empty(transport.Commands);
        }
    }

    public class CapabilityDegradationTests
    {
        /// <summary>
        /// A transport that responds to getvar queries with a fixed table and records writes.
        /// </summary>
        private sealed class VarTableTransport : IFastbootTransport
        {
            private readonly Dictionary<string, string> _vars;
            public List<string> Commands { get; } = new();

            public VarTableTransport(Dictionary<string, string> vars)
            {
                _vars = vars;
            }

            public byte[] Read(int length)
            {
                // After each command write, respond based on the last command.
                string last = Commands.Count > 0 ? Commands[^1] : "";
                if (last.StartsWith("getvar:", StringComparison.OrdinalIgnoreCase))
                {
                    string key = last.Substring("getvar:".Length);
                    if (_vars.TryGetValue(key, out string? value)) return Encoding.UTF8.GetBytes("OKAY" + value);
                    return Encoding.UTF8.GetBytes("FAILunknown variable");
                }
                return Encoding.UTF8.GetBytes("OKAY");
            }

            public long Write(byte[] data, int length)
            {
                Commands.Add(Encoding.UTF8.GetString(data, 0, length));
                return length;
            }

            public void Dispose() { }
        }

        [Fact]
        public void ProbeCapabilities_PopulatesKnownVariables()
        {
            var transport = new VarTableTransport(new Dictionary<string, string>
            {
                ["version"] = "0.4",
                ["version-bootloader"] = "U-Boot 2023.01",
                ["max-download-size"] = "0x10000000",
                ["is-userspace"] = "no",
                ["has-slot:boot"] = "no",
                ["slot-count"] = "1",
                ["current-slot"] = "_a",
                ["super-partition-name"] = "super",
                ["is-logical:super"] = "no",
            });
            var driver = new FastbootDriver(transport);

            var caps = driver.ProbeCapabilities();

            Assert.True(caps.IsProbed);
            Assert.Equal("0.4", caps.ProtocolVersion);
            Assert.Equal("U-Boot 2023.01", caps.BootloaderVersion);
            Assert.Equal(0x10000000, caps.MaxDownloadSize);
            Assert.False(caps.IsUserspace);
            Assert.False(caps.SupportsSlots);
            Assert.Equal(1, caps.SlotCount);
            Assert.Equal("a", caps.CurrentSlot);
            Assert.Equal("super", caps.SuperPartitionName);
            Assert.True(caps.SupportsLogicalPartitions);
        }

        [Fact]
        public void ProbeCapabilities_UnsupportedVariables_AreLeftUnset()
        {
            var transport = new VarTableTransport(new Dictionary<string, string>());
            var driver = new FastbootDriver(transport);

            var caps = driver.ProbeCapabilities();

            Assert.True(caps.IsProbed);
            Assert.Null(caps.ProtocolVersion);
            Assert.Null(caps.MaxDownloadSize);
            Assert.Null(caps.IsUserspace);
            Assert.Null(caps.SuperPartitionName);
            Assert.False(caps.SupportsLogicalPartitions);
        }

        [Fact]
        public void IsUserspace_UsesProbedCapabilities_WithoutExtraQueries()
        {
            var transport = new VarTableTransport(new Dictionary<string, string>
            {
                ["is-userspace"] = "yes",
            });
            var driver = new FastbootDriver(transport);

            driver.ProbeCapabilities();
            int commandCount = transport.Commands.Count;
            Assert.True(driver.IsUserspace());
            Assert.Equal(commandCount, transport.Commands.Count); // no extra getvar
        }

        [Fact]
        public void GetMaxDownloadSize_UsesProbedCapabilities()
        {
            var transport = new VarTableTransport(new Dictionary<string, string>
            {
                ["max-download-size"] = "0x80000000",
            });
            var driver = new FastbootDriver(transport);

            driver.ProbeCapabilities();
            Assert.Equal(0x80000000, driver.GetMaxDownloadSize());
        }

        [Fact]
        public void IsLogical_DegradesToFalse_WhenDeviceHasNoLogicalPartitions()
        {
            var transport = new VarTableTransport(new Dictionary<string, string>
            {
                ["is-logical:super"] = "no",
            });
            var driver = new FastbootDriver(transport);

            driver.ProbeCapabilities();
            Assert.False(driver.IsLogical("super"));
        }

        [Fact]
        public void EnsureUserspace_Throws_WhenIsUserspaceUnsupported()
        {
            var transport = new VarTableTransport(new Dictionary<string, string>());
            var driver = new FastbootDriver(transport);

            driver.ProbeCapabilities();
            Assert.Throws<NotSupportedException>(() => driver.EnsureUserspace());
            Assert.DoesNotContain(transport.Commands, c => c.StartsWith("reboot-fastboot", StringComparison.OrdinalIgnoreCase));
        }
    }

    public class DeviceProfileLoaderTests
    {
        [Fact]
        public void LoadFromFile_ParsesValidManifest()
        {
            string path = Path.Combine(Path.GetTempPath(), "fastboot_profiles_test.json");
            File.WriteAllText(path, """
            {
              "profiles": [
                { "name": "Kindle", "vendorId": "0x1949", "productId": "0x0006" },
                { "name": "Rockchip", "vendorId": "0x2207", "productId": null },
                { "name": "Huawei", "vendorId": "12d1", "productId": "0x5526" }
              ]
            }
            """);

            try
            {
                var profiles = Usb.DeviceProfileLoader.LoadFromFile(path);

                Assert.Equal(3, profiles.Count);
                Assert.Equal((ushort)0x1949, profiles[0].VendorId);
                Assert.Equal((ushort?)0x0006, profiles[0].ProductId);
                Assert.Equal("Kindle", profiles[0].Name);
                Assert.Equal((ushort)0x2207, profiles[1].VendorId);
                Assert.Null(profiles[1].ProductId);
                Assert.Equal((ushort)0x12D1, profiles[2].VendorId);
                Assert.Equal((ushort?)0x5526, profiles[2].ProductId);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LoadFromFile_SkipsMalformedEntries()
        {
            string path = Path.Combine(Path.GetTempPath(), "fastboot_profiles_bad.json");
            File.WriteAllText(path, """
            {
              "profiles": [
                { "name": "NoVid", "productId": "0x0006" },
                { "name": "BadHex", "vendorId": "0xZZZZ", "productId": "0x0006" },
                { "name": "ZeroVid", "vendorId": "0x0000", "productId": null },
                { "name": "Good", "vendorId": "0x1234", "productId": "0x5678" }
              ]
            }
            """);

            try
            {
                var profiles = Usb.DeviceProfileLoader.LoadFromFile(path);

                Assert.Single(profiles);
                Assert.Equal((ushort)0x1234, profiles[0].VendorId);
                Assert.Equal((ushort?)0x5678, profiles[0].ProductId);
                Assert.Equal("Good", profiles[0].Name);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LoadFromFile_MissingFile_ReturnsEmpty()
        {
            string path = Path.Combine(Path.GetTempPath(), "fastboot_profiles_missing_" + Guid.NewGuid().ToString("N") + ".json");
            var profiles = Usb.DeviceProfileLoader.LoadFromFile(path);
            Assert.Empty(profiles);
        }

        [Fact]
        public void LoadFromFile_CorruptJson_ReturnsEmpty()
        {
            string path = Path.Combine(Path.GetTempPath(), "fastboot_profiles_corrupt.json");
            File.WriteAllText(path, "{ not valid json !!!");

            try
            {
                var profiles = Usb.DeviceProfileLoader.LoadFromFile(path);
                Assert.Empty(profiles);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void UsbManager_LoadDeviceProfilesFromFile_ReplacesKnownProfiles()
        {
            string path = Path.Combine(Path.GetTempPath(), "fastboot_profiles_replace.json");
            File.WriteAllText(path, """
            { "profiles": [ { "name": "Only", "vendorId": "0xBEEF", "productId": null } ] }
            """);

            try
            {
                Usb.UsbManager.LoadDeviceProfilesFromFile(path);
                Assert.Single(Usb.UsbManager.KnownDeviceProfiles);
                Assert.Equal(0xBEEF, Usb.UsbManager.KnownDeviceProfiles[0].VendorId);
            }
            finally
            {
                File.Delete(path);
                Usb.UsbManager.KnownDeviceProfiles = Usb.UsbManager.DefaultDeviceProfiles;
            }
        }
    }
}
