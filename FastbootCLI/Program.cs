using FirmwareKit.Comm.Fastboot;
using FirmwareKit.Comm.Fastboot.Usb;
using FirmwareKit.Comm.Fastboot.Usb.libusbdotnet;
using FirmwareKit.Comm.Fastboot.Usb.Linux;
using FirmwareKit.Comm.Fastboot.Usb.macOS;
using FirmwareKit.Comm.Fastboot.Usb.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FastbootCLI
{
    class Program
    {
        private static string? serial = null;
        private static string? slot = null;
        private static bool wipe = false;
        private static bool forceLibUsb = false;

        static void Main(string[] args)
        {
            forceLibUsb = Environment.GetEnvironmentVariable("SHARP_FASTBOOT_LIBUSB") == "1";
            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            int i = 0;
            while (i < args.Length && args[i].StartsWith("-"))
            {
                string arg = args[i++];
                if (arg == "-s" && i < args.Length)
                {
                    serial = args[i++];
                }
                else if (arg == "-w")
                {
                    wipe = true;
                }
                else if (arg == "-l" || arg == "--libusb")
                {
                    forceLibUsb = true;
                }
                else if (arg == "--slot" && i < args.Length)
                {
                    slot = args[i++];
                }
                else if (arg == "--version")
                {
                    ShowVersion();
                    return;
                }
                else if (arg == "-h" || arg == "--help")
                {
                    ShowHelp();
                    return;
                }
                else
                {
                    i--;
                    break;
                }
            }

            if (i >= args.Length)
            {
                ShowHelp();
                return;
            }

            string command = args[i++];
            List<string> commandArgs = args.Skip(i).ToList();

            try
            {
                ExecuteCommand(command, commandArgs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("fastboot: error: " + ex.Message);
                Environment.Exit(1);
            }
        }

        static void ExecuteCommand(string command, List<string> args)
        {
            if (command == "devices")
            {
                ListDevices();
                return;
            }

            if (command == "version")
            {
                ShowVersion();
                return;
            }

            if (command == "help")
            {
                ShowHelp();
                return;
            }

            FastbootUtil? util = ConnectDevice();
            if (util == null)
            {
                throw new Exception("no devices found" + (serial != null ? " with serial " + serial : ""));
            }

            util.ReceivedFromDevice += (s, e) =>
            {
                if (e.NewInfo != null)
                {
                    Console.Error.WriteLine("(bootloader) " + e.NewInfo);
                }
            };

            util.CurrentStepChanged += (s, e) => Console.Error.WriteLine(e + "...");

            bool showProgress = !string.Equals(command, "stage", StringComparison.OrdinalIgnoreCase);

            util.DataTransferProgressChanged += (s, e) =>
            {
                if (!showProgress) return;
                var (current, total) = e;
                double percent = (double)current / total * 100;
                Console.Error.Write($"\rProgress: {percent:F1}% ({current}/{total} bytes)    ");
                if (current == total) Console.Error.WriteLine();
            };

            Stopwatch sw = Stopwatch.StartNew();

            switch (command)
            {
                case "getvar":
                    if (args.Count == 0) throw new Exception("getvar requires a variable name");
                    if (args[0] == "all")
                    {
                        util.GetVarAll();
                    }
                    else
                    {
                        util.GetVar(args[0]);
                    }
                    break;
                case "reboot":
                    if (args.Count == 0 || args[0] == "system") util.RawCommand("reboot").ThrowIfError();
                    else if (args[0] == "bootloader") util.Reboot("bootloader").ThrowIfError();
                    else if (args[0] == "recovery") util.Reboot("recovery").ThrowIfError();
                    else if (args[0] == "fastboot") util.Reboot("fastboot").ThrowIfError();
                    else util.RawCommand("reboot-" + args[0]).ThrowIfError();
                    break;
                case "reboot-bootloader":
                    util.Reboot("bootloader").ThrowIfError();
                    break;
                case "reboot-recovery":
                    util.Reboot("recovery").ThrowIfError();
                    break;
                case "reboot-fastboot":
                    util.Reboot("fastboot").ThrowIfError();
                    break;
                case "flash":
                    {
                        if (args.Count < 1) throw new Exception("flash: usage: flash <partition> [ <filename> ]");
                        string part = args[0];
                        string? file = args.Count > 1 ? args[1] : null;

                        if (file == null)
                        {
                            file = FindImageFile(part);
                            if (file == null) throw new Exception($"flash: '{part}' not found in current directory and ANDROID_PRODUCT_OUT is not set or file not found.");
                        }

                        if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && (part == "update" || part == "zip"))
                        {
                            util.FlashZip(file);
                        }
                        else if (part == "raw")
                        {
                            if (args.Count < 2) throw new Exception("flash raw: usage: flash raw <partition> <kernel> [ <ramdisk> ]");
                            string targetPartition = args[1];
                            string kernelPath = args[2];
                            string? ramdiskPath = args.Count > 3 ? args[3] : null;
                            util.FlashRaw(targetPartition, kernelPath, ramdiskPath).ThrowIfError();
                        }
                        else if (part == "vbmeta")
                        {
                            if (file == null) throw new Exception("flash vbmeta: usage: flash vbmeta <filename> [ --disable-verity ] [ --disable-verification ]");
                            bool disableVerity = args.Contains("--disable-verity");
                            bool disableVerification = args.Contains("--disable-verification");
                            util.FlashVbmeta(part, file, disableVerity, disableVerification).ThrowIfError();
                        }
                        else if (part == "bootconfig")
                        {
                            if (file == null || args.Count < 3) throw new Exception("bootconfig: usage: flash bootconfig <partition> <key> <value>");
                            string targetPartition = file;
                            string key = args[2];
                            string val = args[3];
                            throw new NotSupportedException("To modify bootconfig, use a local BootImage and then flash the image.");
                        }
                        else
                        {
                            string target = part;
                            string? slotOverride = slot;
                            if (args.Contains("--slot"))
                            {
                                int idx = args.IndexOf("--slot");
                                if (idx + 1 < args.Count) slotOverride = args[idx + 1];
                            }
                            util.FlashImage(target, file, slotOverride);
                        }
                        Console.WriteLine("OKAY");
                    }
                    break;
                case "gsi":
                    if (args.Count == 0) throw new Exception("gsi: usage: gsi <wipe|disable|status>");
                    if (args[0] == "wipe") util.GsiWipe().ThrowIfError();
                    else if (args[0] == "disable") util.GsiDisable().ThrowIfError();
                    else if (args[0] == "status") util.GsiStatus().ThrowIfError();
                    else throw new Exception("unknown gsi command: " + args[0]);
                    break;
                case "update-super":
                    if (args.Count < 2) throw new Exception("update-super: usage: update-super <partition> <filename>");
                    util.UpdateSuper(args[0], args[1]).ThrowIfError();
                    Console.WriteLine("OKAY");
                    break;
                case "wipe-super":
                    if (args.Count == 0) throw new Exception("wipe-super: usage: wipe-super <partition>");
                    util.WipeSuper(args[0]).ThrowIfError();
                    Console.WriteLine("OKAY");
                    break;
                case "erase":
                    if (args.Count == 0) throw new Exception("erase: usage: erase <partition>");
                    {
                        string target = args[0];
                        if (slot != null && util.HasSlot(args[0])) target = args[0] + "_" + slot;
                        util.ErasePartition(target).ThrowIfError();
                        Console.WriteLine("OKAY");
                    }
                    break;
                case "format":
                    if (args.Count == 0) throw new Exception("format: usage: format <partition>");
                    {
                        string target = args[0];
                        if (target.Contains(":")) target = target.Split(':').Last();
                        if (slot != null && util.HasSlot(target)) target = target + "_" + slot;
                        util.FormatPartition(target).ThrowIfError();
                        Console.WriteLine("OKAY");
                    }
                    break;
                case "continue":
                    util.Continue().ThrowIfError();
                    break;
                case "set_active":
                    if (args.Count == 0) throw new Exception("set_active: usage: set_active <slot>");
                    util.SetActiveSlot(args[0]).ThrowIfError();
                    Console.WriteLine("OKAY");
                    break;
                case "oem":
                    if (args.Count == 0) throw new Exception("oem: usage: oem <command>");
                    util.OemCommand(string.Join(" ", args)).ThrowIfError();
                    Console.WriteLine("OKAY");
                    break;
                case "flashing":
                    if (args.Count == 0) throw new Exception("flashing: usage: flashing <subcommand>");
                    util.FlashingCommand(string.Join("_", args)).ThrowIfError();
                    Console.WriteLine("OKAY");
                    break;
                case "snapshot-update":
                    if (args.Count > 0 && args[0] == "merge")
                    {
                        if (args.Contains("--wait"))
                        {
                            util.WaitForSnapshotMerge();
                        }
                        else
                        {
                            util.SnapshotUpdate("merge").ThrowIfError();
                        }
                    }
                    else if (args.Count == 0) util.SnapshotUpdate().ThrowIfError();
                    else util.SnapshotUpdate(args[0]).ThrowIfError();
                    Console.WriteLine("OKAY");
                    break;
                case "fetch":
                    if (args.Count < 2) throw new Exception("fetch: usage: fetch <partition> <filename>");
                    util.Fetch(args[0], args[1]);
                    break;
                case "get_staged":
                    if (args.Count == 0) throw new Exception("get_staged: usage: get_staged <filename>");
                    util.GetStaged(args[0]);
                    break;
                case "create-logical-partition":
                    if (args.Count < 2) throw new Exception("create-logical-partition: usage: create-logical-partition <name> <size>");
                    util.CreateLogicalPartition(args[0], ParseSize(args[1]));
                    break;
                case "delete-logical-partition":
                    if (args.Count == 0) throw new Exception("delete-logical-partition: usage: delete-logical-partition <name>");
                    util.DeleteLogicalPartition(args[0]);
                    break;
                case "resize-logical-partition":
                    if (args.Count < 2) throw new Exception("resize-logical-partition: usage: resize-logical-partition <name> <size>");
                    util.ResizeLogicalPartition(args[0], ParseSize(args[1]));
                    break;
                case "boot":
                    if (args.Count < 1) throw new Exception("boot: usage: boot <kernel> [ <ramdisk> [ <second> ] ]");
                    {
                        string kernel = args[0];
                        if (!File.Exists(kernel)) kernel = FindImageFile(kernel) ?? kernel;

                        string? ramdisk = args.Count > 1 ? args[1] : null;
                        if (ramdisk != null && !File.Exists(ramdisk)) ramdisk = FindImageFile(ramdisk) ?? ramdisk;

                        string? second = args.Count > 2 ? args[2] : null;
                        if (second != null && !File.Exists(second)) second = FindImageFile(second) ?? second;

                        util.Boot(kernel, ramdisk, second).ThrowIfError();
                        Console.WriteLine("OKAY");
                    }
                    break;
                case "flashall":
                    {
                        string? productOut = Environment.GetEnvironmentVariable("ANDROID_PRODUCT_OUT");
                        if (string.IsNullOrEmpty(productOut))
                        {
                            productOut = Directory.GetCurrentDirectory();
                        }
                        bool skipSecondary = args.Contains("--skip-secondary");
                        bool force = args.Contains("-f") || args.Contains("--force");
                        util.FlashAll(productOut, wipe, skipSecondary, force);
                        Console.WriteLine("OKAY");
                    }
                    break;
                case "stage":
                    if (args.Count == 0) throw new Exception("stage: usage: stage <filename>");
                    string stageFile = args[0];
                    if (!File.Exists(stageFile)) stageFile = FindImageFile(stageFile) ?? stageFile;
                    long stageSize = new FileInfo(stageFile).Length;
                    Stopwatch stageSw = Stopwatch.StartNew();
                    using (var fs = File.OpenRead(stageFile))
                    {
                        util.DownloadData(fs, fs.Length).ThrowIfError();
                    }
                    stageSw.Stop();
                    Console.Error.WriteLine($"Sending '{stageFile}' ({stageSize / 1024} KB)                  OKAY [  {stageSw.Elapsed.TotalSeconds:F3}s]");
                    break;
                case "update":
                    if (args.Count == 0) throw new Exception("update: usage: update <filename.zip>");
                    util.FlashZip(args[0]);
                    Console.WriteLine("OKAY");
                    break;
                default:
                    throw new Exception("unknown command: " + command);
            }


            if (wipe)
            {
                Console.Error.WriteLine("Wiping userdata and cache...");
                try { util.FormatPartition("userdata"); } catch { }
                try { util.FormatPartition("cache"); } catch { }
            }

            sw.Stop();
            Console.Error.WriteLine($"Finished. Total time: {sw.Elapsed.TotalSeconds:F3}s");
        }

        static long ParseSize(string sizeStr)
        {
            if (sizeStr.EndsWith("K", StringComparison.OrdinalIgnoreCase)) return long.Parse(sizeStr.Substring(0, sizeStr.Length - 1)) * 1024;
            if (sizeStr.EndsWith("M", StringComparison.OrdinalIgnoreCase)) return long.Parse(sizeStr.Substring(0, sizeStr.Length - 1)) * 1024 * 1024;
            if (sizeStr.EndsWith("G", StringComparison.OrdinalIgnoreCase)) return long.Parse(sizeStr.Substring(0, sizeStr.Length - 1)) * 1024 * 1024 * 1024;
            return long.Parse(sizeStr);
        }

        static List<UsbDevice> GetAllDevices()
        {
            if (forceLibUsb)
            {
                return LibUsbFinder.FindDevice();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return WinUSBFinder.FindDevice();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return LinuxUsbFinder.FindDevice();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return MacOSUsbFinder.FindDevice();
            }
            return LibUsbFinder.FindDevice();
        }

        static void ListDevices()
        {
            var devices = GetAllDevices();
            foreach (var dev in devices)
            {
                if (dev.GetSerialNumber() != 0)
                {
                    dev.Dispose();
                    continue;
                }

                if (string.IsNullOrEmpty(dev.SerialNumber) || dev.SerialNumber == "unknown")
                {
                    dev.Dispose();
                    continue;
                }

                Console.WriteLine($"{dev.SerialNumber}\tfastboot");
                dev.Dispose();
            }
        }

        static FastbootUtil? ConnectDevice()
        {
            return FastbootUtil.WaitForDevice(GetAllDevices, serial, 3);
        }

        static void ShowHelp()
        {
            Console.WriteLine("usage: fastboot [ <option> ] <command>");
            Console.WriteLine("");
            Console.WriteLine("commands:");
            Console.WriteLine("  update <filename.zip>                    Reflash device from update.zip");
            Console.WriteLine("  flashall                                 Flash all images in ANDROID_PRODUCT_OUT");
            Console.WriteLine("  flash <partition> [ <filename> ]         Write a file to a flash partition");
            Console.WriteLine("  flash raw <partition> <kernel> [ <rdisk> ] Create bootimage and flash it");
            Console.WriteLine("  flash vbmeta <filename> [--disable-verity] [--disable-verification]");
            Console.WriteLine("  update-super <partition> <filename>      Update super metadata");
            Console.WriteLine("  wipe-super <partition>                   Wipe super metadata");
            Console.WriteLine("  erase <partition>                        Erase a flash partition");
            Console.WriteLine("  format <partition>                       Format a flash partition");
            Console.WriteLine("  getvar <variable>                        Display a bootloader variable");
            Console.WriteLine("  boot <kernel> [ <ramdisk> [ <second> ] ] Download and boot kernel");
            Console.WriteLine("  stage <filename>                         Stage a file into memory");
            Console.WriteLine("  continue                                 Continue with the boot protocol");
            Console.WriteLine("  reboot [bootloader|recovery|fastboot]    Reboot device");
            Console.WriteLine("  reboot-bootloader                        Reboot device into bootloader");
            Console.WriteLine("  set_active <slot>                        Sets the active slot");
            Console.WriteLine("  oem <command>                            Executes an OEM-specific command");
            Console.WriteLine("  flashing <subcommand>                    Executes a flashing command");
            Console.WriteLine("  snapshot-update [cancel|merge]           Virtual A/B snapshot update");
            Console.WriteLine("  fetch <partition> <filename>             Fetch data from a partition");
            Console.WriteLine("  get_staged <filename>                    Read staged data");
            Console.WriteLine("  create-logical-partition <name> <size>   Create a logical partition");
            Console.WriteLine("  delete-logical-partition <name>          Delete a logical partition");
            Console.WriteLine("  resize-logical-partition <name> <size>   Resize a logical partition");
            Console.WriteLine("  devices                                  List all connected devices");
            Console.WriteLine("  version                                  Show version");
            Console.WriteLine("  help                                     Show this help message");
            Console.WriteLine("");
            Console.WriteLine("options:");
            Console.WriteLine("  -w                                       Erase userdata and cache");
            Console.WriteLine("  -s <serial>                              Specify device serial number");
            Console.WriteLine("  -l, --libusb                             Force use libusb implementation");
            Console.WriteLine("  --slot <slot>                            Specify slot name");
            Console.WriteLine("  --version                                Show version");
        }

        static void ShowVersion()
        {
            Console.WriteLine("fastboot version 1.0.0 (FirmwareKit.Comm.Fastboot)");
        }

        static string? FindImageFile(string partition)
        {
            string productOut = Environment.GetEnvironmentVariable("ANDROID_PRODUCT_OUT") ?? "";
            string[] searchPaths = new string[] { Directory.GetCurrentDirectory(), productOut };
            string[] extensions = new string[] { ".img", ".bin" };

            foreach (var path in searchPaths)
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) continue;
                foreach (var ext in extensions)
                {
                    string fullPath = Path.Combine(path, partition + ext);
                    if (File.Exists(fullPath)) return fullPath;
                }
            }
            return null;
        }
    }
}
