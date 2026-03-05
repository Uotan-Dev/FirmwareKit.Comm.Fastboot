using FirmwareKit.Comm.Fastboot;
using FirmwareKit.Comm.Fastboot.Usb;
using FirmwareKit.Comm.Fastboot.Usb.Windows;

namespace FastbootCLI
{
    class Program
    {
        private static string? serial = null;
        private static string? slot = null;

        static void Main(string[] args)
        {
            FastbootDebug.IsEnabled = Environment.GetEnvironmentVariable("FASTBOOT_DEBUG") == "1";

            if (args.Length == 0) { ShowHelp(); return; }

            int i = 0;
            List<string> commandArgs = new List<string>();
            while (i < args.Length)
            {
                string arg = args[i++];
                if (arg == "-s" && i < args.Length) serial = args[i++];
                else if (arg == "--slot" && i < args.Length) slot = args[i++];
                else if (arg == "--debug") FastbootDebug.IsEnabled = true;
                else if (arg == "--version" || arg == "version") { Console.WriteLine("fastboot version 1.1.0"); return; }
                else if (arg == "-h" || arg == "--help" || arg == "help") { ShowHelp(); return; }
                else if (!arg.StartsWith("-"))
                {
                    string command = arg;
                    commandArgs = args.Skip(i).ToList();
                    try { ExecuteCommand(command, commandArgs); }
                    catch (Exception ex)
                    {
                        if (FastbootDebug.IsEnabled) Console.Error.WriteLine("[DEBUG] Exception: " + ex);
                        Console.Error.WriteLine("fastboot: error: " + ex.Message);
                        Environment.Exit(1);
                    }
                    return;
                }
            }
            ShowHelp();
        }

        static void ExecuteCommand(string command, List<string> args)
        {
            if (command == "devices")
            {
                bool verbose = args.Contains("-l");
                foreach (var dev in UsbManager.GetAllDevices())
                {
                    if (verbose) Console.WriteLine($"{dev.SerialNumber}\tfastboot {dev.GetType().Name}");
                    else Console.WriteLine($"{dev.SerialNumber}\tfastboot");
                }
                return;
            }

            var devices = UsbManager.GetAllDevices();
            UsbDevice? target = serial != null ? devices.FirstOrDefault(d => d.SerialNumber == serial) : (devices.Count > 0 ? devices[0] : null);

            if (target == null) throw new Exception("no devices/found");

            using FastbootUtil util = new FastbootUtil(target);
            util.ReceivedFromDevice += (s, e) =>
            {
                if (e.NewInfo != null) Console.Error.WriteLine("(bootloader) " + e.NewInfo);
            };
            util.DataTransferProgressChanged += (s, e) =>
            {
                int percent = (int)(e.Item1 * 100 / e.Item2);
                Console.Write($"\r{command} ({e.Item1}/{e.Item2}) {percent}%    ");
                if (e.Item1 == e.Item2) Console.WriteLine();
            };

            // Process --slot if provided
            string cmdSuffix = "";
            if (!string.IsNullOrEmpty(slot))
            {
                if (slot == "all" || slot == "other" || slot == "a" || slot == "b")
                {
                    // For specific commands, Google fastboot appends slot suffix
                    // We handle it in individual cases if needed or via a global rule
                }
            }

            switch (command)
            {
                case "getvar":
                    if (args.Count == 0) throw new Exception("getvar requires a variable name");
                    if (args[0] == "all") util.GetVarAll();
                    else Console.WriteLine(args[0] + ": " + util.GetVar(args[0]));
                    break;

                case "reboot":
                    string targetStr = args.Count > 0 ? args[0] : "";
                    util.Reboot(targetStr).ThrowIfError();
                    break;

                case "fetch":
                    if (args.Count < 2) throw new Exception("usage: fastboot fetch <partition> <outfile>");
                    util.Fetch(args[0], args[1]).ThrowIfError();
                    break;

                case "flash":
                    if (args.Count < 1) throw new Exception("usage: fastboot flash <partition> [filename]");
                    string part = args[0];
                    string? file = args.Count > 1 ? args[1] : null;
                    if (file == null) throw new Exception("Automatic image discovery from $ANDROID_PRODUCT_OUT not implemented yet. Please specify filename.");
                    if (!File.Exists(file)) throw new Exception($"File not found: {file}");
                    using (var fs = File.OpenRead(file))
                    {
                        util.FlashUnsparseImage(part, fs, fs.Length).ThrowIfError();
                    }
                    break;

                case "flash:raw":
                    if (args.Count < 2) throw new Exception("usage: fastboot flash:raw <partition> <kernel> [ramdisk [second]]");
                    string rawPart = args[0];
                    string rawKernel = args[1];
                    string? rawRamdisk = args.Count > 2 ? args[2] : null;
                    string? rawSecond = args.Count > 3 ? args[3] : null;
                    util.FlashRaw(rawPart, rawKernel, rawRamdisk, rawSecond).ThrowIfError();
                    break;

                case "erase":
                    if (args.Count == 0) throw new Exception("usage: fastboot erase <partition>");
                    util.ErasePartition(args[0]).ThrowIfError();
                    break;

                case "format":
                    if (args.Count == 0) throw new Exception("usage: fastboot format <partition>");
                    util.FormatPartition(args[0]).ThrowIfError();
                    break;

                case "set_active":
                    if (args.Count == 0) throw new Exception("usage: fastboot set_active <slot>");
                    util.SetActiveSlot(args[0]).ThrowIfError();
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
                    util.SnapshotUpdate(sub).ThrowIfError();
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
                    util.BootFile(kernel, ramdisk, second).ThrowIfError();
                    break;

                default:
                    Console.WriteLine("Command not implemented: " + command);
                    break;
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("Usage: fastboot [-s <serial>] [--slot <slot>] [--debug] <command> [args]");
            Console.WriteLine("\nbasics:");
            Console.WriteLine("  devices [-l]                   List connected devices.");
            Console.WriteLine("  getvar <name> | all            Display bootloader variable.");
            Console.WriteLine("  reboot [bootloader|fastboot]   Reboot device.");
            Console.WriteLine("  continue                       Continue with autoboot.");

            Console.WriteLine("\nflashing:");
            Console.WriteLine("  flash <partition> [filename]   Write file to partition.");
            Console.WriteLine("  flash:raw <p> <k> [r [s]]      Create boot image and flash it.");
            Console.WriteLine("  erase <partition>              Erase a flash partition.");
            Console.WriteLine("  format <partition>             Format a flash partition.");
            Console.WriteLine("  set_active <slot>              Set the active slot.");

            Console.WriteLine("\nlocking/unlocking:");
            Console.WriteLine("  flashing lock|unlock           Lock/unlock partitions.");
            Console.WriteLine("  flashing lock_critical|...     Lock/unlock critical partitions.");
            Console.WriteLine("  flashing get_unlock_ability    Check if unlocking is allowed.");

            Console.WriteLine("\nadvanced:");
            Console.WriteLine("  fetch <p> <outfile>            Fetch a partition from device.");
            Console.WriteLine("  oem <command>                  Execute OEM-specific command.");
            Console.WriteLine("  gsi wipe|disable|status        Manage GSI installation.");
            Console.WriteLine("  wipe-super [super_empty]       Wipe the super partition.");
            Console.WriteLine("  snapshot-update cancel|merge   Manage snapshot updates.");

            Console.WriteLine("\nlogical partitions:");
            Console.WriteLine("  create-logical-partition <p> <s>");
            Console.WriteLine("  delete-logical-partition <p>");
            Console.WriteLine("  resize-logical-partition <p> <s>");

            Console.WriteLine("\nboot image:");
            Console.WriteLine("  boot <kernel> [ramdisk [s]]     Download and boot kernel from RAM.");

            Console.WriteLine("\nAndroid Things / Miscellaneous:");
            Console.WriteLine("  stage <filename>               Send file to device for next command.");
            Console.WriteLine("  get_staged <outfile>           Write data staged by last command to file.");
            Console.WriteLine("  upload <name> <outfile>        Legacy upload (e.g. last_kmsg).");
        }
    }
}
