using FirmwareKit.Comm.Fastboot;
using FirmwareKit.Comm.Fastboot.Network;
using FirmwareKit.Comm.Fastboot.Usb;
using System.Globalization;

namespace FastbootCLI
{
    class Program
    {
        private const int DefaultNetworkPort = 5554;

        private static string? serial = null;
        private static string? slot = null;
        private static bool wipeUserdata = false;
        private static bool skipReboot = false;
        private static bool skipSecondary = false;
        private static bool forceFlash = false;
        private static bool disableSuperOptimization = false;
        private static bool excludeDynamicPartitions = false;
        private static bool disableFastbootInfo = false;
        private static bool disableVerity = false;
        private static bool disableVerification = false;
        private static string? fsOptions = null;
        private static long? sparseLimit = null;
        private static bool convertSimgToRaw = !Environment.Is64BitOperatingSystem;

        private static string? defaultDtb = null;
        private static string? defaultCmdline = null;
        private static uint? defaultHeaderVersion = null;
        private static uint? defaultBaseAddr = null;
        private static uint? defaultPageSize = null;
        private static uint? defaultKernelOffset = null;
        private static uint? defaultRamdiskOffset = null;
        private static uint? defaultSecondOffset = null;
        private static uint? defaultTagsOffset = null;
        private static uint? defaultDtbOffset = null;
        private static string? defaultOsVersion = null;
        private static string? defaultOsPatchLevel = null;

        static void Main(string[] args)
        {
            FastbootDebug.IsEnabled = Environment.GetEnvironmentVariable("FASTBOOT_DEBUG") == "1";
            FastbootDebug.Output = message => Console.Error.WriteLine($"[DEBUG] {message}");

            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            int i = 0;
            var pendingCommands = new List<(string Command, List<string> Args)>();
            var commandSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "devices", "getvar", "reboot", "reboot-bootloader", "reboot-fastboot", "reboot-recovery",
                "fetch", "flash", "flashall", "update", "flash:raw", "erase", "format", "set_active",
                "oem", "flashing", "create-logical-partition", "delete-logical-partition", "resize-logical-partition",
                "snapshot-update", "continue", "stage", "get_staged", "upload", "gsi", "wipe-super", "boot",
                "connect", "disconnect", "signature"
            };

            bool IsCommandToken(string token)
                => commandSet.Contains(token) || token.StartsWith("format:", StringComparison.OrdinalIgnoreCase);

            while (i < args.Length && args[i].StartsWith("-"))
            {
                string arg = args[i++];
                if (arg == "-s" && i < args.Length) serial = args[i++];
                else if (arg == "--slot" && i < args.Length) slot = args[i++];
                else if (arg == "-a" || arg == "--set-active" || arg.StartsWith("--set-active=", StringComparison.OrdinalIgnoreCase))
                {
                    if (arg.StartsWith("--set-active=", StringComparison.OrdinalIgnoreCase))
                    {
                        slot = arg.Substring("--set-active=".Length);
                    }
                    else if (i < args.Length && !args[i].StartsWith("-"))
                    {
                        slot = args[i++];
                    }
                }
                else if (arg == "-w") wipeUserdata = true;
                else if (arg == "--skip-reboot") skipReboot = true;
                else if (arg == "--skip-secondary") skipSecondary = true;
                else if (arg == "--force") forceFlash = true;
                else if (arg == "--disable-super-optimization") disableSuperOptimization = true;
                else if (arg == "--exclude-dynamic-partitions") excludeDynamicPartitions = true;
                else if (arg == "--disable-fastboot-info") disableFastbootInfo = true;
                else if (arg == "--fs-options" && i < args.Length) fsOptions = args[i++];
                else if (arg == "--disable-verity") disableVerity = true;
                else if (arg == "--disable-verification") disableVerification = true;
                else if (arg == "-S" && i < args.Length) sparseLimit = ParseSize(args[i++]);
                else if (arg == "--dtb" && i < args.Length) defaultDtb = args[i++];
                else if (arg == "--cmdline" && i < args.Length) defaultCmdline = args[i++];
                else if (arg == "--header-version" && i < args.Length) defaultHeaderVersion = ParseUIntOption("--header-version", args[i++]);
                else if (arg == "--base" && i < args.Length) defaultBaseAddr = ParseUIntOption("--base", args[i++]);
                else if (arg == "--page-size" && i < args.Length) defaultPageSize = ParseUIntOption("--page-size", args[i++]);
                else if (arg == "--kernel-offset" && i < args.Length) defaultKernelOffset = ParseUIntOption("--kernel-offset", args[i++]);
                else if (arg == "--ramdisk-offset" && i < args.Length) defaultRamdiskOffset = ParseUIntOption("--ramdisk-offset", args[i++]);
                else if (arg == "--tags-offset" && i < args.Length) defaultTagsOffset = ParseUIntOption("--tags-offset", args[i++]);
                else if (arg == "--dtb-offset" && i < args.Length) defaultDtbOffset = ParseUIntOption("--dtb-offset", args[i++]);
                else if (arg == "--os-version" && i < args.Length) defaultOsVersion = args[i++];
                else if (arg == "--os-patch-level" && i < args.Length) defaultOsPatchLevel = args[i++];
                else if (arg == "--debug" || arg == "--verbose" || arg == "-v") FastbootDebug.IsEnabled = true;
                else if (arg == "--unbuffered") EnableUnbufferedOutput();
                else if (arg == "--fallback") UsbManager.ForceLibUsb = false;
                else if (arg == "--convert-simg-to-raw") convertSimgToRaw = true;
                else if (arg == "--version" || arg == "version") { Console.Error.WriteLine("fastboot version 1.2.5"); return; }
                else if (arg == "-h" || arg == "--help" || arg == "help") { ShowHelp(); return; }
                else
                {
                    Console.Error.WriteLine("fastboot: error: Unknown option: " + arg);
                    Environment.Exit(1);
                    return;
                }
            }

            while (i < args.Length)
            {
                string token = args[i++];
                string command = token;
                var commandArgs = new List<string>();

                if (token.StartsWith("format:", StringComparison.OrdinalIgnoreCase))
                {
                    command = "format";
                    string spec = token.Substring("format:".Length);
                    if (!string.IsNullOrEmpty(spec)) commandArgs.Add(spec);
                }
                else if (!commandSet.Contains(command))
                {
                    Console.Error.WriteLine("fastboot: error: Unknown command: " + token);
                    Environment.Exit(1);
                    return;
                }

                while (i < args.Length && !IsCommandToken(args[i]))
                {
                    commandArgs.Add(args[i++]);
                }

                pendingCommands.Add((command, commandArgs));
            }

            if (pendingCommands.Count == 0)
            {
                ShowHelp();
                return;
            }

            if (pendingCommands.Count == 1 && pendingCommands[0].Command == "devices")
            {
                ExecuteDeviceList(pendingCommands[0].Args);
                return;
            }

            while (pendingCommands.Count > 0 && (pendingCommands[0].Command == "connect" || pendingCommands[0].Command == "disconnect"))
            {
                ExecuteStandaloneCommand(pendingCommands[0].Command, pendingCommands[0].Args);
                pendingCommands.RemoveAt(0);
            }

            if (pendingCommands.Count == 0)
            {
                return;
            }

            if (pendingCommands.Any(c => c.Command is "devices" or "connect" or "disconnect"))
            {
                Console.Error.WriteLine("fastboot: error: devices/connect/disconnect cannot be mixed with other commands");
                Environment.Exit(1);
                return;
            }

            using FastbootDriver util = OpenTargetDriver();
            util.ConvertSimgToRaw = convertSimgToRaw;
            if (sparseLimit.HasValue)
            {
                FastbootDriver.SparseMaxDownloadSize = Math.Min((long)uint.MaxValue, sparseLimit.Value);
            }

            util.ReceivedFromDevice += (s, e) =>
            {
                if (e.NewInfo != null) Console.Error.WriteLine("(bootloader) " + e.NewInfo);
                if (e.NewText != null) Console.Error.Write(e.NewText);
            };

            util.CommandCompleted += (s, e) =>
            {
                if (e.Quiet) return;

                var command = e.Command;
                var response = e.Response;
                if (response.Result == FastbootState.Fail)
                {
                    if (command.StartsWith("snapshot-update", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.Error.WriteLine($"Snapshot                                           FAILED (remote: '{response.Response}')");
                    }
                    else if (!command.StartsWith("getvar:", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.Error.WriteLine($"FAILED (remote: '{response.Response}')");
                    }
                    return;
                }

                if (command.StartsWith("devices", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(response.Response))
                    {
                        Console.WriteLine(response.Response);
                    }
                    return;
                }

                if (string.IsNullOrEmpty(response.Response)) return;

                if (command.StartsWith("getvar:", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(command, "getvar:all", StringComparison.OrdinalIgnoreCase))
                {
                    string key = command.Substring("getvar:".Length);
                    bool alreadyPrinted = response.Info.Any(x => x.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase));
                    if (!alreadyPrinted)
                    {
                        Console.Error.WriteLine($"{key}: {response.Response}");
                    }
                }
                else if (!command.StartsWith("getvar:all", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine(response.Response);
                }
            };

            util.CurrentStepChanged += (s, step) =>
            {
                if (!string.IsNullOrEmpty(step)) Console.Error.WriteLine(step);
            };

            var stepResults = new List<(string Step, TimeSpan Duration, bool Success)>();
            util.OnStepFinished = (step, duration, success) =>
            {
                if (step.StartsWith("Flash") || step.StartsWith("Flashing"))
                {
                    stepResults.Add((step, duration, success));
                }
            };

            foreach (var cmd in pendingCommands)
            {
                try
                {
                    util.ResetTransport();
                    ExecuteCommand(util, cmd.Command, cmd.Args);
                }
                catch (Exception ex)
                {
                    if (FastbootDebug.IsEnabled) Console.Error.WriteLine("[DEBUG] Exception: " + ex);
                    Console.Error.WriteLine("fastboot: error: " + ex.Message);
                    Environment.Exit(1);
                }
            }

            if (pendingCommands.Any(c => c.Command is "flash" or "flashall" or "update"))
            {
                foreach (var (step, duration, success) in stepResults)
                {
                    Console.Error.WriteLine($"{step,-30} {(success ? "Success" : "Failed"),-4} Time: {duration.TotalSeconds:F2} s");
                }
            }
        }

        static FastbootDriver OpenTargetDriver()
        {
            if (!string.IsNullOrWhiteSpace(serial))
            {
                if (TryOpenNetworkTransport(serial!, out IFastbootTransport? networkTransport, out string? networkError))
                {
                    return new FastbootDriver(networkTransport!);
                }

                if (LooksLikeNetworkEndpoint(serial!))
                {
                    throw new Exception(string.IsNullOrWhiteSpace(networkError) ? "failed to connect network fastboot target" : networkError);
                }
            }

            var devices = UsbManager.GetAllDevices();
            UsbDevice? target = null;

            if (serial != null)
            {
                target = devices.FirstOrDefault(d => string.Equals(d.SerialNumber, serial, StringComparison.OrdinalIgnoreCase));
            }
            else if (devices.Count > 0)
            {
                target = devices[0];
            }

            if (target != null)
            {
                foreach (var dev in devices)
                {
                    if (!ReferenceEquals(dev, target)) dev.Dispose();
                }
                return new FastbootDriver(target);
            }

            foreach (var dev in devices) dev.Dispose();

            if (serial == null)
            {
                if (TryOpenFirstSavedNetworkTarget(out IFastbootTransport? savedTransport))
                {
                    return new FastbootDriver(savedTransport!);
                }

                Console.Error.WriteLine("< waiting for any device >");
                while (true)
                {
                    System.Threading.Thread.Sleep(500);

                    var waitedDevices = UsbManager.GetAllDevices();
                    if (waitedDevices.Count > 0)
                    {
                        var waitedTarget = waitedDevices[0];
                        for (int idx = 1; idx < waitedDevices.Count; idx++) waitedDevices[idx].Dispose();
                        return new FastbootDriver(waitedTarget);
                    }

                    foreach (var dev in waitedDevices) dev.Dispose();

                    if (TryOpenFirstSavedNetworkTarget(out IFastbootTransport? waitTransport))
                    {
                        return new FastbootDriver(waitTransport!);
                    }
                }
            }

            throw new Exception("no devices/found");
        }

        static void ExecuteStandaloneCommand(string command, List<string> args)
        {
            switch (command)
            {
                case "connect":
                    ExecuteConnect(args);
                    break;
                case "disconnect":
                    ExecuteDisconnect(args);
                    break;
                default:
                    throw new Exception("unsupported standalone command: " + command);
            }
        }

        static void ExecuteDeviceList(List<string> args)
        {
            bool verbose = args.Contains("-l");
            foreach (var dev in UsbManager.GetAllDevices())
            {
                if (verbose) Console.WriteLine($"{dev.SerialNumber}\tfastboot {dev.GetType().Name}");
                else Console.WriteLine($"{dev.SerialNumber}\tfastboot");
                dev.Dispose();
            }

            foreach (var endpoint in LoadSavedNetworkTargets())
            {
                if (verbose) Console.WriteLine($"{endpoint}\tfastboot network");
                else Console.WriteLine($"{endpoint}\tfastboot");
            }
        }

        static void ExecuteConnect(List<string> args)
        {
            if (args.Count != 1)
            {
                throw new Exception("usage: fastboot connect [tcp:|udp:]HOST[:PORT]");
            }

            string endpoint = NormalizeNetworkEndpoint(args[0]);
            if (!TryOpenNetworkTransport(endpoint, out IFastbootTransport? transport, out string? error))
            {
                throw new Exception(string.IsNullOrWhiteSpace(error) ? "failed to connect" : error);
            }

            transport!.Dispose();

            var endpoints = LoadSavedNetworkTargets();
            if (!endpoints.Contains(endpoint, StringComparer.OrdinalIgnoreCase))
            {
                endpoints.Add(endpoint);
                SaveNetworkTargets(endpoints);
            }

            Console.Error.WriteLine("connected " + endpoint);
        }

        static void ExecuteDisconnect(List<string> args)
        {
            if (args.Count > 1)
            {
                throw new Exception("usage: fastboot disconnect [tcp:|udp:]HOST[:PORT]");
            }

            if (args.Count == 0)
            {
                SaveNetworkTargets(new List<string>());
                Console.Error.WriteLine("disconnected all network fastboot targets");
                return;
            }

            string endpoint = NormalizeNetworkEndpoint(args[0]);
            var endpoints = LoadSavedNetworkTargets();
            int removed = endpoints.RemoveAll(x => string.Equals(x, endpoint, StringComparison.OrdinalIgnoreCase));
            SaveNetworkTargets(endpoints);

            if (removed > 0) Console.Error.WriteLine("disconnected " + endpoint);
            else Console.Error.WriteLine("no such connection: " + endpoint);
        }

        static bool TryOpenFirstSavedNetworkTarget(out IFastbootTransport? transport)
        {
            foreach (var endpoint in LoadSavedNetworkTargets())
            {
                if (TryOpenNetworkTransport(endpoint, out transport, out _))
                {
                    return true;
                }
            }

            transport = null;
            return false;
        }

        static bool TryOpenNetworkTransport(string endpoint, out IFastbootTransport? transport, out string? error)
        {
            transport = null;
            error = null;

            if (!TryParseNetworkEndpoint(endpoint, out string scheme, out string host, out int port, out string parseError))
            {
                error = parseError;
                return false;
            }

            try
            {
                transport = scheme == "tcp" ? new TcpTransport(host, port) : new UdpTransport(host, port);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                transport = null;
                return false;
            }
        }

        static bool LooksLikeNetworkEndpoint(string value)
        {
            return value.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("udp:", StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeNetworkEndpoint(string endpoint)
        {
            if (!TryParseNetworkEndpoint(endpoint, out string scheme, out string host, out int port, out string error))
            {
                throw new Exception(error);
            }

            return $"{scheme}:{host}:{port}";
        }

        static bool TryParseNetworkEndpoint(string endpoint, out string scheme, out string host, out int port, out string error)
        {
            scheme = "";
            host = "";
            port = DefaultNetworkPort;
            error = "";

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                error = "network endpoint is empty";
                return false;
            }

            int firstColon = endpoint.IndexOf(':');
            if (firstColon <= 0)
            {
                error = "network endpoint must be tcp:HOST[:PORT] or udp:HOST[:PORT]";
                return false;
            }

            scheme = endpoint.Substring(0, firstColon).ToLowerInvariant();
            if (scheme != "tcp" && scheme != "udp")
            {
                error = "network scheme must be tcp or udp";
                return false;
            }

            string rest = endpoint.Substring(firstColon + 1);
            if (string.IsNullOrWhiteSpace(rest))
            {
                error = "network endpoint missing host";
                return false;
            }

            int portSep = rest.LastIndexOf(':');
            if (portSep > 0)
            {
                host = rest.Substring(0, portSep);
                string portText = rest.Substring(portSep + 1);
                if (!int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port <= 0 || port > 65535)
                {
                    error = "invalid network port: " + portText;
                    return false;
                }
            }
            else
            {
                host = rest;
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                error = "network endpoint missing host";
                return false;
            }

            return true;
        }

        static string GetNetworkStorePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FastbootCLI");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "network_targets.txt");
        }

        static List<string> LoadSavedNetworkTargets()
        {
            string path = GetNetworkStorePath();
            if (!File.Exists(path)) return new List<string>();

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (TryParseNetworkEndpoint(trimmed, out string scheme, out string host, out int port, out _))
                {
                    set.Add($"{scheme}:{host}:{port}");
                }
            }

            return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static void SaveNetworkTargets(List<string> endpoints)
        {
            string path = GetNetworkStorePath();
            var normalized = endpoints
                .Select(NormalizeNetworkEndpoint)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            File.WriteAllLines(path, normalized);
        }

        static void EnableUnbufferedOutput()
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(stdout);
            var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
            Console.SetError(stderr);
        }

        static long ParseSize(string sizeStr)
        {
            long multiplier = 1;
            char last = char.ToLower(sizeStr[^1]);
            if (last == 'k') { multiplier = 1024; sizeStr = sizeStr[..^1]; }
            else if (last == 'm') { multiplier = 1024 * 1024; sizeStr = sizeStr[..^1]; }
            else if (last == 'g') { multiplier = 1024 * 1024 * 1024; sizeStr = sizeStr[..^1]; }
            return long.Parse(sizeStr, CultureInfo.InvariantCulture) * multiplier;
        }

        static uint ParseUIntOption(string optionName, string value)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
                {
                    return hex;
                }
            }

            if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed))
            {
                return parsed;
            }

            throw new Exception($"invalid value for {optionName}: {value}");
        }

        static uint EncodeOsVersion(string? osVersionText, string? osPatchLevelText)
        {
            int major = 0;
            int minor = 0;
            int patch = 0;

            if (!string.IsNullOrWhiteSpace(osVersionText))
            {
                string[] parts = osVersionText.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0) int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out major);
                if (parts.Length > 1) int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minor);
                if (parts.Length > 2) int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out patch);
            }

            major = Math.Clamp(major, 0, 127);
            minor = Math.Clamp(minor, 0, 127);
            patch = Math.Clamp(patch, 0, 127);

            int year = 2000;
            int month = 0;
            if (!string.IsNullOrWhiteSpace(osPatchLevelText))
            {
                string[] patchParts = osPatchLevelText.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (patchParts.Length > 0 && int.TryParse(patchParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)) year = y;
                if (patchParts.Length > 1 && int.TryParse(patchParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int m)) month = m;
            }

            year = Math.Clamp(year, 2000, 2127);
            month = Math.Clamp(month, 0, 12);

            uint encodedVersion = (uint)((major << 14) | (minor << 7) | patch);
            uint encodedPatch = (uint)(((year - 2000) << 4) | month);
            return (encodedVersion << 11) | encodedPatch;
        }

        static void ParseBootStyleArgs(
            List<string> args,
            bool includePartition,
            out string? partition,
            out string kernel,
            out string? ramdisk,
            out string? second,
            out string? dtb,
            out string? cmdline,
            out uint headerVersion,
            out uint baseAddr,
            out uint pageSize,
            out uint kernelOffset,
            out uint ramdiskOffset,
            out uint secondOffset,
            out uint tagsOffset,
            out uint dtbOffset,
            out uint osVersion)
        {
            partition = null;
            kernel = "";
            ramdisk = null;
            second = null;
            dtb = defaultDtb;
            cmdline = defaultCmdline;
            headerVersion = defaultHeaderVersion ?? 0;
            baseAddr = defaultBaseAddr ?? 0x10000000;
            pageSize = defaultPageSize ?? 2048;
            kernelOffset = defaultKernelOffset ?? 0x00008000;
            ramdiskOffset = defaultRamdiskOffset ?? 0x01000000;
            secondOffset = defaultSecondOffset ?? 0x00F00000;
            tagsOffset = defaultTagsOffset ?? 0x00000100;
            dtbOffset = defaultDtbOffset ?? 0x01100000;
            string? localOsVersion = defaultOsVersion;
            string? localOsPatchLevel = defaultOsPatchLevel;
            osVersion = EncodeOsVersion(localOsVersion, localOsPatchLevel);

            var positional = new List<string>();
            for (int idx = 0; idx < args.Count; idx++)
            {
                string token = args[idx];

                if (token == "--dtb" && idx + 1 < args.Count) { dtb = args[++idx]; continue; }
                if (token == "--cmdline" && idx + 1 < args.Count) { cmdline = args[++idx]; continue; }
                if (token == "--header-version" && idx + 1 < args.Count) { headerVersion = ParseUIntOption("--header-version", args[++idx]); continue; }
                if (token == "--base" && idx + 1 < args.Count) { baseAddr = ParseUIntOption("--base", args[++idx]); continue; }
                if (token == "--page-size" && idx + 1 < args.Count) { pageSize = ParseUIntOption("--page-size", args[++idx]); continue; }
                if (token == "--kernel-offset" && idx + 1 < args.Count) { kernelOffset = ParseUIntOption("--kernel-offset", args[++idx]); continue; }
                if (token == "--ramdisk-offset" && idx + 1 < args.Count) { ramdiskOffset = ParseUIntOption("--ramdisk-offset", args[++idx]); continue; }
                if (token == "--tags-offset" && idx + 1 < args.Count) { tagsOffset = ParseUIntOption("--tags-offset", args[++idx]); continue; }
                if (token == "--dtb-offset" && idx + 1 < args.Count) { dtbOffset = ParseUIntOption("--dtb-offset", args[++idx]); continue; }
                if (token == "--os-version" && idx + 1 < args.Count)
                {
                    localOsVersion = args[++idx];
                    osVersion = EncodeOsVersion(localOsVersion, localOsPatchLevel);
                    continue;
                }
                if (token == "--os-patch-level" && idx + 1 < args.Count)
                {
                    localOsPatchLevel = args[++idx];
                    osVersion = EncodeOsVersion(localOsVersion, localOsPatchLevel);
                    continue;
                }

                positional.Add(token);
            }

            if (includePartition)
            {
                if (positional.Count < 2) throw new Exception("missing partition or kernel argument");
                partition = positional[0];
                kernel = positional[1];
                if (positional.Count > 2) ramdisk = positional[2];
                if (positional.Count > 3) second = positional[3];
                if (positional.Count > 4) throw new Exception("too many positional arguments");
                return;
            }

            if (positional.Count < 1) throw new Exception("missing kernel argument");
            kernel = positional[0];
            if (positional.Count > 1) ramdisk = positional[1];
            if (positional.Count > 2) second = positional[2];
            if (positional.Count > 3) throw new Exception("too many positional arguments");
        }

        static void ExecuteCommand(FastbootDriver util, string command, List<string> args)
        {
            if (command == "devices")
            {
                ExecuteDeviceList(args);
                return;
            }

            if (wipeUserdata && (command == "flashall" || command == "update"))
            {
                Console.Error.WriteLine("Wiping userdata/cache as requested by -w...");
                util.ErasePartition("userdata");
                util.FormatPartition("userdata");
                util.ErasePartition("cache");
                util.FormatPartition("cache");
            }

            string GetPartition(string baseName)
            {
                if (string.IsNullOrEmpty(slot) || slot == "all")
                {
                    if (util.HasSlot(baseName))
                    {
                        string current = util.GetCurrentSlot();
                        if (!string.IsNullOrEmpty(current)) return baseName + "_" + current;
                    }
                    return baseName;
                }

                if (slot == "other")
                {
                    string current = util.GetCurrentSlot();
                    string other = (current == "a") ? "b" : "a";
                    return baseName + "_" + other;
                }

                if (util.HasSlot(baseName)) return baseName + "_" + slot;
                return baseName;
            }

            if (command == "set_active")
            {
                string? targetSlot = args.Count > 0 ? args[0] : slot;
                if (string.IsNullOrEmpty(targetSlot))
                {
                    string? current = util.GetVar("current-slot");
                    targetSlot = (current == "a") ? "b" : "a";
                }
                util.SetActiveSlot(targetSlot).ThrowIfError();
                return;
            }

            switch (command)
            {
                case "getvar":
                    if (args.Count == 0) throw new Exception("getvar requires a variable name");
                    if (args[0] == "all") util.GetVarAll();
                    else util.GetVar(args[0]);
                    break;

                case "reboot":
                    string targetStr = args.Count > 0 ? args[0] : "";
                    if (targetStr == "fastboot")
                    {
                        Console.Error.WriteLine("waiting for any device >");
                        util.EnsureUserspace();
                    }
                    else
                    {
                        util.Reboot(targetStr).ThrowIfError();
                    }
                    break;

                case "reboot-bootloader":
                    util.Reboot("bootloader").ThrowIfError();
                    break;

                case "reboot-fastboot":
                    Console.Error.WriteLine("waiting for any device >");
                    util.EnsureUserspace();
                    break;

                case "reboot-recovery":
                    util.Reboot("recovery").ThrowIfError();
                    break;

                case "fetch":
                    if (args.Count < 2) throw new Exception("usage: fastboot fetch <partition> <outfile> [offset [size]]");
                    string fetchPart = GetPartition(args[0]);
                    if (args.Count > 2)
                    {
                        long offset = ParseSize(args[2]);
                        long fetchSize = args.Count > 3 ? ParseSize(args[3]) : -1;
                        util.Fetch(fetchPart, args[1], offset, fetchSize).ThrowIfError();
                    }
                    else
                    {
                        util.Fetch(fetchPart, args[1]).ThrowIfError();
                    }
                    break;

                case "flash":
                    if (args.Count == 0) throw new Exception("usage: fastboot flash [--disable-verity] [--disable-verification] <partition> [filename]");

                    bool flashDisableVerity = false;
                    bool flashDisableVerification = false;
                    var flashArgs = new List<string>();
                    foreach (var a in args)
                    {
                        if (a == "--disable-verity") flashDisableVerity = true;
                        else if (a == "--disable-verification") flashDisableVerification = true;
                        else flashArgs.Add(a);
                    }

                    flashDisableVerity |= disableVerity;
                    flashDisableVerification |= disableVerification;

                    if (flashArgs.Count == 0)
                        throw new Exception("usage: fastboot flash [--disable-verity] [--disable-verification] <partition> [filename]");

                    string flashPartition = flashArgs[0];
                    string flashFile;
                    if (flashArgs.Count > 1)
                    {
                        flashFile = flashArgs[1];
                    }
                    else
                    {
                        string? productOutForFlash = Environment.GetEnvironmentVariable("ANDROID_PRODUCT_OUT");
                        if (string.IsNullOrEmpty(productOutForFlash))
                            throw new Exception("filename is required when ANDROID_PRODUCT_OUT is not set");
                        flashFile = Path.Combine(productOutForFlash, flashPartition + ".img");
                    }

                    if (!File.Exists(flashFile)) throw new FileNotFoundException(flashFile);

                    string? slotOverride = slot;
                    if (slotOverride == "other")
                    {
                        string currentSlot = util.GetCurrentSlot();
                        slotOverride = currentSlot == "a" ? "b" : "a";
                    }

                    bool isVbmeta = flashPartition.StartsWith("vbmeta", StringComparison.OrdinalIgnoreCase);
                    if (isVbmeta && (flashDisableVerity || flashDisableVerification))
                    {
                        string vbmetaTarget = GetPartition(flashPartition);
                        util.FlashVbmeta(vbmetaTarget, flashFile, flashDisableVerity, flashDisableVerification).ThrowIfError();
                    }
                    else if (flashPartition.StartsWith("vendor_boot:ramdisk", StringComparison.OrdinalIgnoreCase))
                    {
                        string vendorPartition = GetPartition("vendor_boot");
                        string tempOriginal = Path.Combine(Path.GetTempPath(), "vendor_boot_orig_" + Guid.NewGuid().ToString("N") + ".img");
                        string tempRepacked = Path.Combine(Path.GetTempPath(), "vendor_boot_repacked_" + Guid.NewGuid().ToString("N") + ".img");
                        try
                        {
                            util.Fetch(vendorPartition, tempOriginal).ThrowIfError();
                            using (var originalStream = File.OpenRead(tempOriginal))
                            {
                                var vendorBoot = BootImage.Parse(originalStream);
                                vendorBoot.Ramdisk = File.ReadAllBytes(flashFile);
                                if (!string.IsNullOrWhiteSpace(defaultDtb) && File.Exists(defaultDtb))
                                {
                                    vendorBoot.Dtb = File.ReadAllBytes(defaultDtb);
                                }

                                using var repacked = File.Create(tempRepacked);
                                vendorBoot.Serialize(repacked);
                            }

                            util.FlashImage("vendor_boot", tempRepacked, slotOverride);
                        }
                        finally
                        {
                            try { if (File.Exists(tempOriginal)) File.Delete(tempOriginal); } catch { }
                            try { if (File.Exists(tempRepacked)) File.Delete(tempRepacked); } catch { }
                        }
                    }
                    else
                    {
                        util.FlashImage(flashPartition, flashFile, slotOverride);
                    }
                    break;

                case "flashall":
                    string? productOut = Environment.GetEnvironmentVariable("ANDROID_PRODUCT_OUT");
                    if (string.IsNullOrEmpty(productOut)) throw new Exception("ANDROID_PRODUCT_OUT not set. Please use: fastboot update ZIP");
                    util.FlashAll(productOut, false, skipSecondary, forceFlash, !disableSuperOptimization, disableVerity, disableVerification, disableFastbootInfo, excludeDynamicPartitions);
                    if (!skipReboot) util.Reboot("");
                    break;

                case "update":
                    if (args.Count == 0) throw new Exception("usage: fastboot update <zip>");
                    util.FlashUpdateZip(args[0], skipSecondary, disableVerity, disableVerification, forceFlash, !disableSuperOptimization, disableFastbootInfo, excludeDynamicPartitions);
                    if (!skipReboot) util.Reboot("");
                    break;

                case "flash:raw":
                    if (args.Count < 2) throw new Exception("usage: fastboot flash:raw <partition> <kernel> [ramdisk [second]] [options]");
                    ParseBootStyleArgs(args, true,
                        out string? rawPartition,
                        out string rawKernel,
                        out string? rawRamdisk,
                        out string? rawSecond,
                        out string? rawDtb,
                        out string? rawCmdline,
                        out uint rawHeaderVersion,
                        out uint rawBaseAddr,
                        out uint rawPageSize,
                        out uint rawKernelOffset,
                        out uint rawRamdiskOffset,
                        out uint rawSecondOffset,
                        out uint rawTagsOffset,
                        out uint rawDtbOffset,
                        out uint rawOsVersion);
                    util.FlashRaw(GetPartition(rawPartition!), rawKernel, rawRamdisk, rawSecond, rawDtb, rawCmdline,
                        rawHeaderVersion, rawBaseAddr, rawPageSize, rawKernelOffset, rawRamdiskOffset, rawSecondOffset,
                        rawTagsOffset, rawDtbOffset, rawOsVersion).ThrowIfError();
                    break;

                case "erase":
                    if (args.Count == 0) throw new Exception("usage: fastboot erase <partition>");
                    util.ErasePartition(GetPartition(args[0])).ThrowIfError();
                    break;

                case "format":
                    if (args.Count == 0) throw new Exception("usage: fastboot format[:FS_TYPE[:SIZE]] <partition>");
                    string formatPartition;
                    string? formatFsType = null;
                    long? formatSize = null;

                    if (args.Count == 1)
                    {
                        formatPartition = args[0];
                    }
                    else if (args.Count == 2)
                    {
                        string spec = args[0];
                        formatPartition = args[1];
                        var parts = spec.Split(':', StringSplitOptions.None);
                        if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) formatFsType = parts[0];
                        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) formatSize = ParseSize(parts[1]);
                    }
                    else
                    {
                        throw new Exception("usage: fastboot format[:FS_TYPE[:SIZE]] <partition>");
                    }

                    util.FormatPartition(GetPartition(formatPartition), formatFsType, formatSize, fsOptions).ThrowIfError();
                    break;

                case "set_active":
                    string? saSlot = args.Count > 0 ? args[0] : slot;
                    if (string.IsNullOrEmpty(saSlot))
                    {
                        string? current = util.GetVar("current-slot");
                        saSlot = (current == "a") ? "b" : "a";
                    }
                    util.SetActiveSlot(saSlot).ThrowIfError();
                    break;

                case "oem":
                    if (args.Count == 0) throw new Exception("usage: fastboot oem <command>");
                    util.OemCommand(string.Join(" ", args)).ThrowIfError();
                    break;

                case "flashing":
                    if (args.Count == 0) throw new Exception("usage: fastboot flashing lock|unlock|lock_critical|unlock_critical|get_unlock_ability");
                    util.FlashingCommand(string.Join(" ", args)).ThrowIfError();
                    break;

                case "create-logical-partition":
                    if (args.Count < 2) throw new Exception("usage: fastboot create-logical-partition <partition> <size>");
                    if (!long.TryParse(args[1], out long size)) throw new Exception("Invalid size");
                    util.CreateLogicalPartition(args[0], size).ThrowIfError();
                    break;

                case "delete-logical-partition":
                    if (args.Count == 0) throw new Exception("usage: fastboot delete-logical-partition <partition>");
                    util.DeleteLogicalPartition(args[0]).ThrowIfError();
                    break;

                case "resize-logical-partition":
                    if (args.Count < 2) throw new Exception("usage: fastboot resize-logical-partition <partition> <size>");
                    if (!long.TryParse(args[1], out long rsize)) throw new Exception("Invalid size");
                    util.ResizeLogicalPartition(args[0], rsize).ThrowIfError();
                    break;

                case "snapshot-update":
                    string sub = args.Count > 0 ? args[0] : "cancel";
                    if (sub == "cancel" || sub == "merge") util.SnapshotUpdate(sub).ThrowIfError();
                    else throw new Exception("usage: fastboot snapshot-update cancel|merge");
                    break;

                case "continue":
                    util.Continue().ThrowIfError();
                    break;

                case "stage":
                    if (args.Count == 0) throw new Exception("usage: fastboot stage <filename>");
                    util.Stage(File.ReadAllBytes(args[0])).ThrowIfError();
                    break;

                case "get_staged":
                    if (args.Count == 0) throw new Exception("usage: fastboot get_staged <outfile>");
                    util.GetStaged(args[0]);
                    break;

                case "upload":
                    if (args.Count < 2) throw new Exception("usage: fastboot upload <name> <outfile>");
                    util.Upload(args[0], args[1]).ThrowIfError();
                    break;

                case "gsi":
                    if (args.Count == 0) throw new Exception("usage: fastboot gsi wipe|disable|status");
                    util.GsiCommand(args[0]).ThrowIfError();
                    break;

                case "wipe-super":
                    string? emptyImg = args.Count > 0 ? args[0] : null;
                    if (emptyImg != null) util.UpdateSuper("super", emptyImg, true).ThrowIfError();
                    else util.WipeSuper("super").ThrowIfError();
                    break;

                case "boot":
                    if (args.Count == 0) throw new Exception("usage: fastboot boot <kernel> [ramdisk [second]] [options]");
                    ParseBootStyleArgs(args, false,
                        out _,
                        out string kernel,
                        out string? ramdisk,
                        out string? second,
                        out string? dtb,
                        out string? cmdline,
                        out uint headerVersion,
                        out uint baseAddr,
                        out uint pageSize,
                        out uint kernelOffset,
                        out uint ramdiskOffset,
                        out uint secondOffset,
                        out uint tagsOffset,
                        out uint dtbOffset,
                        out uint osVersion);
                    util.Boot(kernel, ramdisk, second, dtb, cmdline, headerVersion, baseAddr, pageSize,
                        kernelOffset, ramdiskOffset, secondOffset, tagsOffset, dtbOffset, osVersion).ThrowIfError();
                    break;

                case "signature":
                    if (args.Count == 0) throw new Exception("usage: fastboot signature <signature-file>");
                    util.Signature(File.ReadAllBytes(args[0])).ThrowIfError();
                    break;

                default:
                    throw new NotSupportedException("Command not implemented: " + command);
            }
        }

        static void ShowHelp()
        {
            Console.Error.WriteLine("Usage: fastboot [-s <serial>] [--slot <slot>] [-w] [-S <size>] [--skip-reboot] [--debug] <command> [args]");
            Console.Error.WriteLine("\noptions:");
            Console.Error.WriteLine("  -w                             Wipe userdata and cache after flashing.");
            Console.Error.WriteLine("  -s <serial>                    Specify device serial (USB or tcp:HOST[:PORT] / udp:HOST[:PORT]).");
            Console.Error.WriteLine("  --slot <slot>                  Specify active slot (a/b/all/other).");
            Console.Error.WriteLine("  -a, --set-active[=<slot>]      Sets the active slot. If no slot is provided,");
            Console.Error.WriteLine("                                 it will set the inactive slot to active.");
            Console.Error.WriteLine("  -S <size>[k|m|g]               Break into sparse files no larger than SIZE.");
            Console.Error.WriteLine("  --skip-reboot                  Don't reboot device after flashing all.");
            Console.Error.WriteLine("  --skip-secondary               Don't flash secondary slots in flashall/update.");
            Console.Error.WriteLine("  --force                        Ignore compatibility checks for flashall/update.");
            Console.Error.WriteLine("  --disable-super-optimization   Disable optimized super-partition flashing.");
            Console.Error.WriteLine("  --exclude-dynamic-partitions   Skip flashing logical dynamic partitions.");
            Console.Error.WriteLine("  --disable-fastboot-info        Ignore fastboot-info.txt and use image scan fallback.");
            Console.Error.WriteLine("  --fs-options <opt>             File system options for format (e.g. casefold).");
            Console.Error.WriteLine("  --disable-verity               Disable dm-verity in vbmeta images.");
            Console.Error.WriteLine("  --disable-verification         Disable AVB verification in vbmeta images.");
            Console.Error.WriteLine("  --dtb <file>                   Default DTB file for boot/flash:raw.");
            Console.Error.WriteLine("  --cmdline <text>               Default kernel cmdline for boot/flash:raw.");
            Console.Error.WriteLine("  --base <addr>                  Default base address for boot/flash:raw.");
            Console.Error.WriteLine("  --page-size <bytes>            Default page size for boot/flash:raw.");
            Console.Error.WriteLine("  --header-version <ver>         Default boot image header version for boot/flash:raw.");
            Console.Error.WriteLine("  --kernel-offset <addr>         Kernel offset for boot/flash:raw image header.");
            Console.Error.WriteLine("  --ramdisk-offset <addr>        Ramdisk offset for boot/flash:raw image header.");
            Console.Error.WriteLine("  --tags-offset <addr>           Tags offset for boot/flash:raw image header.");
            Console.Error.WriteLine("  --dtb-offset <addr>            DTB offset for boot/flash:raw image header.");
            Console.Error.WriteLine("  --os-version X[.Y[.Z]]         Android OS version for boot image header.");
            Console.Error.WriteLine("  --os-patch-level YYYY-MM[-DD]  Android OS patch level for boot image header.");
            Console.Error.WriteLine("  --unbuffered                   Disable stdio buffering.");
            Console.Error.WriteLine("  --verbose, -v                  Verbose logging output.");
            Console.Error.WriteLine("  --convert-simg-to-raw          Convert sparse image to raw image before flashing to avoid 32-bit addressing issues. Defaults to true on 32-bit systems.");
            Console.Error.WriteLine("  --fallback                     Use platform native USB backend instead of libusb (on Linux libusb is default).");

            Console.Error.WriteLine("\nbasics:");
            Console.Error.WriteLine("  devices [-l]                   List connected devices.");
            Console.Error.WriteLine("  connect [tcp:|udp:]HOST[:PORT] Add and validate a network fastboot target.");
            Console.Error.WriteLine("  disconnect [tcp:|udp:]HOST[:PORT] Remove one or all saved network targets.");
            Console.Error.WriteLine("  getvar <name> | all            Display bootloader variable.");
            Console.Error.WriteLine("  reboot [bootloader|fastboot|recovery] Reboot device.");
            Console.Error.WriteLine("  continue                       Continue with autoboot.");

            Console.Error.WriteLine("\nflashing:");
            Console.Error.WriteLine("  update <zip>                   Flash all partitions from a zip file.");
            Console.Error.WriteLine("  flashall                       Flash all partitions from $ANDROID_PRODUCT_OUT.");
            Console.Error.WriteLine("  flash <partition> [filename]   Write file to partition.");
            Console.Error.WriteLine("  flash [--disable-verity] [--disable-verification] vbmeta [filename]");
            Console.Error.WriteLine("  flash vendor_boot:ramdisk <ramdisk_file>");
            Console.Error.WriteLine("  flash:raw <p> <k> [r [s]] [--dtb file] [--cmdline text] [--base addr]");
            Console.Error.WriteLine("                                 [--page-size bytes] [--header-version ver] [--kernel-offset addr]");
            Console.Error.WriteLine("                                 [--ramdisk-offset addr] [--tags-offset addr] [--dtb-offset addr]");
            Console.Error.WriteLine("                                 [--os-version X.Y.Z] [--os-patch-level YYYY-MM]");
            Console.Error.WriteLine("  erase <partition>              Erase a flash partition.");
            Console.Error.WriteLine("  format[:FS_TYPE[:SIZE]] <p>    Format a flash partition.");
            Console.Error.WriteLine("  set_active <slot>              Set the active slot.");

            Console.Error.WriteLine("\nlocking/unlocking:");
            Console.Error.WriteLine("  flashing lock|unlock           Lock/unlock partitions.");
            Console.Error.WriteLine("  flashing lock_critical|...     Lock/unlock critical partitions.");
            Console.Error.WriteLine("  flashing get_unlock_ability    Check if unlocking is allowed.");

            Console.Error.WriteLine("\nadvanced:");
            Console.Error.WriteLine("  fetch <p> <outfile> [off [sz]] Fetch a partition from device.");
            Console.Error.WriteLine("  oem <command>                  Execute OEM-specific command.");
            Console.Error.WriteLine("  gsi wipe|disable|status        Manage GSI installation.");
            Console.Error.WriteLine("  wipe-super [super_empty]       Wipe the super partition.");
            Console.Error.WriteLine("  snapshot-update cancel|merge   Manage snapshot updates.");

            Console.Error.WriteLine("\nlogical partitions:");
            Console.Error.WriteLine("  create-logical-partition <p> <s>");
            Console.Error.WriteLine("  delete-logical-partition <p>");
            Console.Error.WriteLine("  resize-logical-partition <p> <s>");

            Console.Error.WriteLine("\nboot image:");
            Console.Error.WriteLine("  boot <k> [r [s]] [--dtb file] [--cmdline text] [--base addr]");
            Console.Error.WriteLine("       [--page-size bytes] [--header-version ver] [--kernel-offset addr]");
            Console.Error.WriteLine("       [--ramdisk-offset addr] [--tags-offset addr] [--dtb-offset addr]");
            Console.Error.WriteLine("       [--os-version X.Y.Z] [--os-patch-level YYYY-MM]");
            Console.Error.WriteLine("                                 Download and boot kernel from RAM.");

            Console.Error.WriteLine("\nAndroid Things / Miscellaneous:");
            Console.Error.WriteLine("  stage <filename>               Send file to device for next command.");
            Console.Error.WriteLine("  get_staged <outfile>           Write data staged by last command to file.");
            Console.Error.WriteLine("  upload <name> <outfile>        Legacy upload (e.g. last_kmsg).");
            Console.Error.WriteLine("  signature <file>               Send signature blob and install it.");
        }
    }
}
