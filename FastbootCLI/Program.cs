using FirmwareKit.Comm.Fastboot;
using FirmwareKit.Comm.Fastboot.Usb;
using FirmwareKit.Comm.Fastboot.Usb.Windows;

namespace FastbootCLI
{
    class Program
    {
        private static string? serial = null;

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
                else if (arg == "--debug") FastbootDebug.IsEnabled = true;
                else if (arg == "--version") { Console.WriteLine("fastboot version 1.0.1"); return; }
                else if (arg == "-h" || arg == "--help") { ShowHelp(); return; }
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
                foreach (var dev in WinUSBFinder.FindDevice())
                    Console.WriteLine($"{dev.SerialNumber}\tfastboot");
                return;
            }

            var devices = WinUSBFinder.FindDevice();
            UsbDevice? target = serial != null ? devices.FirstOrDefault(d => d.SerialNumber == serial) : (devices.Count > 0 ? devices[0] : null);

            if (target == null) throw new Exception("no devices/found");

            using FastbootUtil util = new FastbootUtil(target);
            util.ReceivedFromDevice += (s, e) => { if (e.NewInfo != null) Console.Error.WriteLine("(bootloader) " + e.NewInfo); };

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
                default:
                    Console.WriteLine("Command not implemented: " + command);
                    break;
            }
        }

        static void ShowHelp() { Console.WriteLine("Usage: fastboot [-s <serial>] [--debug] <command> [args]"); }
    }
}
