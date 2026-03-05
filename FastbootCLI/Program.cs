using FirmwareKit.Comm.Fastboot;
using FirmwareKit.Comm.Fastboot.Backend.Usb;


namespace FastbootCLI
{
    class Program
    {
        private static string? serial = null;
        private static string? slot = null;
        private static bool wipeUserdata = false;
        private static bool skipReboot = false;
        private static bool force = false;
        private static string? fsOptions = null;
        private static long? sparseLimit = null;

        static void Main(string[] args)
        {
            FastbootDebug.IsEnabled = Environment.GetEnvironmentVariable("FASTBOOT_DEBUG") == "1";

            if (args.Length == 0) { ShowHelp(); return; }

            int i = 0;
            List<(string Command, List<string> Args)> pendingCommands = new List<(string, List<string>)>();

            while (i < args.Length)
            {
                string arg = args[i++];
                if (arg == "-s" && i < args.Length) serial = args[i++];
                else if (arg == "--slot" && i < args.Length) slot = args[i++];
                else if (arg == "-a" || arg == "--set-active")
                {
                    // If next arg doesn't start with '-', it's the slot name
                    if (i < args.Length && !args[i].StartsWith("-")) slot = args[i++];
                    // Note: if no slot provided, AOSP logic handles active toggle in ExecuteCommand
                }
                else if (arg == "-w") wipeUserdata = true;
                else if (arg == "--skip-reboot") skipReboot = true;
                else if (arg == "--force") force = true;
                else if (arg == "--fs-options" && i < args.Length) fsOptions = args[i++];
                else if (arg == "-S" && i < args.Length)
                {
                    string sizeStr = args[i++];
                    sparseLimit = ParseSize(sizeStr);
                }
                else if (arg == "--debug") FastbootDebug.IsEnabled = true;
                else if (arg == "--libusb") UsbManager.ForceLibUsb = true;
                else if (arg == "--version" || arg == "version") { Console.WriteLine("fastboot version 1.2.5"); return; }
                else if (arg == "-h" || arg == "--help" || arg == "help") { ShowHelp(); return; }
                else if (!arg.StartsWith("-"))
                {
                    // This is a command (like 'devices', 'flash', 'getvar')
                    string command = arg;

                    // Collect arguments for this specific command until next arg starting with '-'
                    List<string> commandArgs = new List<string>();
                    while (i < args.Length && !args[i].StartsWith("-"))
                    {
                        commandArgs.Add(args[i++]);
                    }
                    pendingCommands.Add((command, commandArgs));
                }
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

            var devices = UsbManager.GetAllDevices();
            UsbDevice? target = serial != null ? devices.FirstOrDefault(d => d.SerialNumber == serial) : (devices.Count > 0 ? devices[0] : null);

            if (target == null)
            {
                Console.Error.WriteLine("fastboot: error: no devices/found");
                Environment.Exit(1);
            }

            using FastbootUtil util = new FastbootUtil(target);
            if (sparseLimit.HasValue) FastbootUtil.SparseMaxDownloadSize = (int)Math.Min(int.MaxValue, sparseLimit.Value);

            util.ReceivedFromDevice += (s, e) =>
            {
                if (e.NewInfo != null) Console.Error.WriteLine("(bootloader) " + e.NewInfo);
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
        }

        static void ExecuteDeviceList(List<string> args)
        {
            bool verbose = args.Contains("-l");
            foreach (var dev in UsbManager.GetAllDevices())
            {
                if (verbose) Console.WriteLine($"{dev.SerialNumber}\tfastboot {dev.GetType().Name}");
                else Console.WriteLine($"{dev.SerialNumber}\tfastboot");
            }
        }

        static long ParseSize(string sizeStr)
        {
            long multiplier = 1;
            char last = char.ToLower(sizeStr[^1]);
            if (last == 'k') { multiplier = 1024; sizeStr = sizeStr[..^1]; }
            else if (last == 'm') { multiplier = 1024 * 1024; sizeStr = sizeStr[..^1]; }
            else if (last == 'g') { multiplier = 1024 * 1024 * 1024; sizeStr = sizeStr[..^1]; }
            return long.Parse(sizeStr) * multiplier;
        }

        static void ExecuteCommand(FastbootUtil util, string command, List<string> args)
        {
            if (command == "devices")
            {
                ExecuteDeviceList(args);
                return;
            }

            util.DataTransferProgressChanged += (s, e) =>
            {
                int percent = (int)(e.Item1 * 100 / e.Item2);
                Console.Error.Write($"\r{command} ({e.Item1}/{e.Item2}) {percent}%    ");
                if (e.Item1 == e.Item2) Console.Error.WriteLine();
            };

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
                    util.Reboot(targetStr).ThrowIfError();
                    break;

                case "reboot-bootloader":
                    util.Reboot("bootloader").ThrowIfError();
                    break;

                case "reboot-fastboot":
                    util.Reboot("fastboot").ThrowIfError();
                    break;

                case "reboot-recovery":
                    util.Reboot("recovery").ThrowIfError();
                    break;

                case "fetch":
                    if (args.Count < 2) throw new Exception("usage: fastboot fetch <partition> <outfile> [offset [size]]");
                    string fetchPart = GetPartition(args[0]);
                    long offset = args.Count > 2 ? ParseSize(args[2]) : 0;
                    long fetchSize = args.Count > 3 ? ParseSize(args[3]) : 0;
                    if (offset != 0 || fetchSize != 0)
                        util.Fetch(fetchPart, args[1], offset, fetchSize).ThrowIfError();
                    else
                        util.Fetch(fetchPart, args[1]).ThrowIfError();
                    break;

                case "flash":
                    bool disableVerity = args.Contains("--disable-verity");
                    bool disableVerification = args.Contains("--disable-verification");
                    var flashArgs = args.Where(a => !a.StartsWith("--")).ToList();
                    if (flashArgs.Count < 1) throw new Exception("usage: fastboot flash <partition> [filename]");
                    string part = GetPartition(flashArgs[0]);
                    string? file = flashArgs.Count > 1 ? flashArgs[1] : null;

                    if (file == null)
                    {
                        string? envOut = Environment.GetEnvironmentVariable("ANDROID_PRODUCT_OUT");
                        if (!string.IsNullOrEmpty(envOut))
                        {
                            string imgName = $"{flashArgs[0]}.img";
                            string candidate = Path.Combine(envOut, imgName);
                            if (File.Exists(candidate)) file = candidate;
                        }
                    }

                    if (file == null) throw new Exception("Could not find image. Please specify filename or set $ANDROID_PRODUCT_OUT.");
                    if (!File.Exists(file)) throw new Exception($"File not found: {file}");

                    if (part.StartsWith("vbmeta") && (disableVerity || disableVerification))
                        util.FlashVbmeta(part, file, disableVerity, disableVerification).ThrowIfError();
                    else
                    {
                        if (force)
                        {
                            util.OemCommand("snapshot-update cancel").ThrowIfError();
                        }

                        using var fs = File.OpenRead(file);
                        util.FlashUnsparseImage(part, fs, fs.Length).ThrowIfError();
                    }
                    if (!skipReboot && (part.StartsWith("boot") || part.StartsWith("system"))) Console.Error.WriteLine("Note: Device may need a manual reboot or use 'fastboot reboot'.");
                    break;

                case "flashall":
                    string? productOut = Environment.GetEnvironmentVariable("ANDROID_PRODUCT_OUT");
                    if (string.IsNullOrEmpty(productOut)) throw new Exception("ANDROID_PRODUCT_OUT not set. Please use: fastboot update ZIP");
                    util.FlashFromDirectory(productOut);
                    if (!skipReboot) util.Reboot("");
                    break;

                case "update":
                    if (args.Count == 0) throw new Exception("usage: fastboot update <zip>");
                    util.FlashUpdateZip(args[0]);
                    if (!skipReboot) util.Reboot("");
                    break;

                case "flash:raw":
                    if (args.Count < 2) throw new Exception("usage: fastboot flash:raw <partition> <kernel> [ramdisk [second]]");
                    string rawPart = GetPartition(args[0]);
                    string rawKernel = args[1];
                    string? rawRamdisk = args.Count > 2 ? args[2] : null;
                    string? rawSecond = args.Count > 3 ? args[3] : null;
                    util.FlashRaw(rawPart, rawKernel, rawRamdisk, rawSecond).ThrowIfError();
                    break;

                case "erase":
                    if (args.Count == 0) throw new Exception("usage: fastboot erase <partition>");
                    util.ErasePartition(GetPartition(args[0])).ThrowIfError();
                    break;

                case "format":
                    if (args.Count == 0) throw new Exception("usage: fastboot format <partition>");
                    util.FormatPartition(GetPartition(args[0])).ThrowIfError();
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
                    util.RawCommand("delete-logical-partition:" + args[0]).ThrowIfError();
                    break;

                case "resize-logical-partition":
                    if (args.Count < 2) throw new Exception("usage: fastboot resize-logical-partition <partition> <size>");
                    if (!long.TryParse(args[1], out long rsize)) throw new Exception("Invalid size");
                    util.RawCommand($"resize-logical-partition:{args[0]}:{rsize}").ThrowIfError();
                    break;

                case "snapshot-update":
                    string sub = args.Count > 0 ? args[0] : "cancel";
                    if (sub == "cancel" || sub == "merge")
                        util.SnapshotUpdate(sub).ThrowIfError();
                    else
                        throw new Exception("usage: fastboot snapshot-update cancel|merge");
                    break;

                case "continue":
                    util.Continue().ThrowIfError();
                    break;

                case "stage":
                    if (args.Count == 0) throw new Exception("usage: fastboot stage <filename>");
                    byte[] stageData = File.ReadAllBytes(args[0]);
                    util.Stage(stageData).ThrowIfError();
                    break;

                case "get_staged":
                    if (args.Count == 0) throw new Exception("usage: fastboot get_staged <outfile>");
                    using (var ofs = File.Create(args[0]))
                    {
                        util.UploadData("get_staged", ofs).ThrowIfError();
                    }
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
                    else util.RawCommand("wipe-super").ThrowIfError();
                    break;

                case "boot":
                    if (args.Count == 0) throw new Exception("usage: fastboot boot <kernel> [ramdisk [second]]");
                    string kernel = args[0];
                    string? ramdisk = args.Count > 1 ? args[1] : null;
                    string? second = args.Count > 2 ? args[2] : null;
                    util.Boot(kernel, ramdisk, second).ThrowIfError();
                    break;

                default:
                    Console.Error.WriteLine("Command not implemented: " + command);
                    break;
            }
        }

        static void ShowHelp()
        {
            Console.Error.WriteLine("Usage: fastboot [-s <serial>] [--slot <slot>] [-w] [-S <size>] [--skip-reboot] [--debug] <command> [args]");
            Console.Error.WriteLine("\noptions:");
            Console.Error.WriteLine("  -w                             Wipe userdata and cache after flashing.");
            Console.Error.WriteLine("  -s <serial>                    Specify USB device serial.");
            Console.Error.WriteLine("  --slot <slot>                  Specify active slot (a/b/all/other).");
            Console.Error.WriteLine("  -a, --set-active[=<slot>]      Sets the active slot. If no slot is provided,");
            Console.Error.WriteLine("                                 it will set the inactive slot to active.");
            Console.Error.WriteLine("  -S <size>[k|m|g]               Break into sparse files no larger than SIZE.");
            Console.Error.WriteLine("  --skip-reboot                  Don't reboot device after flashing all.");
            Console.Error.WriteLine("  --force                        Force execute command (e.g. skip snapshot check).");
            Console.Error.WriteLine("  --fs-options <opt>             File system options for format (e.g. casefold).");

            Console.Error.WriteLine("\nbasics:");
            Console.Error.WriteLine("  devices [-l]                   List connected devices.");
            Console.Error.WriteLine("  getvar <name> | all            Display bootloader variable.");
            Console.Error.WriteLine("  reboot [bootloader|fastboot|recovery] Reboot device.");
            Console.Error.WriteLine("  continue                       Continue with autoboot.");

            Console.Error.WriteLine("\nflashing:");
            Console.Error.WriteLine("  update <zip>                   Flash all partitions from a zip file.");
            Console.Error.WriteLine("  flashall                       Flash all partitions from $ANDROID_PRODUCT_OUT.");
            Console.Error.WriteLine("  flash <partition> [filename]   Write file to partition.");
            Console.Error.WriteLine("  flash [--disable-verity] [--disable-verification] vbmeta [filename]");
            Console.Error.WriteLine("  flash:raw <p> <k> [r [s]]      Create boot image and flash it.");
            Console.Error.WriteLine("  erase <partition>              Erase a flash partition.");
            Console.Error.WriteLine("  format <partition>             Format a flash partition.");
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
            Console.Error.WriteLine("  boot <kernel> [ramdisk [s]]     Download and boot kernel from RAM.");

            Console.Error.WriteLine("\nAndroid Things / Miscellaneous:");
            Console.Error.WriteLine("  stage <filename>               Send file to device for next command.");
            Console.Error.WriteLine("  get_staged <outfile>           Write data staged by last command to file.");
            Console.Error.WriteLine("  upload <name> <outfile>        Legacy upload (e.g. last_kmsg).");
        }
    }
}


