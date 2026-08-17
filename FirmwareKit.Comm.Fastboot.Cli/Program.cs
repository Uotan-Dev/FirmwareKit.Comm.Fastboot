using CommandLine;
using FirmwareKit.Comm.Fastboot.Cli.Options;
using FirmwareKit.Comm.Fastboot.Network;
using FirmwareKit.Comm.Fastboot.Usb;
using System.Globalization;

namespace FirmwareKit.Comm.Fastboot.Cli;

class Program
{
    private const int DefaultNetworkPort = 5554;

    // AOSP fastboot.cpp 输出模型：Status() 打印 "%-50s " 状态前缀并记录起始时刻，
    // Epilog() 在命令结束后打印 "OKAY [%7.3fs]" 或 "FAILED (...)"。
    private static readonly System.Diagnostics.Stopwatch StatusTimer = new();
    private static bool StatusPending;

    private static void Status(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            Console.Error.Write(string.Format(CultureInfo.InvariantCulture, "{0,-50} ", message));
        }
        StatusTimer.Restart();
        StatusPending = true;
    }

    private static void Epilog(bool ok, string? error = null)
    {
        if (!StatusPending) return;
        if (ok)
        {
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "OKAY [{0,7:F3}s]", StatusTimer.Elapsed.TotalSeconds));
        }
        else
        {
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "FAILED ({0})", error ?? ""));
        }
        StatusPending = false;
    }

    // AOSP DumpInfo() 中的分隔线与对齐标签是逐字直出（不走 Status 状态行），
    // "waiting for any device >" 也是 AOSP 原样打印的提示行。
    // 注意：flash/erase 等步骤消息（如 "Sending raw image to boot..."）含 "..." 后缀，
    // 但必须走 Status 状态行并在命令结束后得到 OKAY/FAILED，因此不能用 "..." 一刀切。
    private static bool IsPlainStep(string message)
    {
        return message.StartsWith("----", StringComparison.Ordinal) ||
               message.StartsWith("waiting for", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Bootloader Version", StringComparison.Ordinal) ||
               message.StartsWith("Baseband Version", StringComparison.Ordinal) ||
               message.StartsWith("Serial Number", StringComparison.Ordinal);
    }

    private static readonly HashSet<string> CommandTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "devices", "getvar", "reboot", "reboot-bootloader", "reboot-fastboot", "reboot-recovery",
        "fetch", "flash", "flashall", "update", "flash:raw", "erase", "format", "set_active",
        "oem", "flashing", "create-logical-partition", "delete-logical-partition", "resize-logical-partition",
        "snapshot-update", "continue", "stage", "get_staged", "upload", "gsi", "wipe-super", "boot",
        "connect", "disconnect", "signature", "sideload", "shutdown", "stash", "update-super"
    };

    static void Main(string[] args)
    {
        FastbootDebug.IsEnabled = Environment.GetEnvironmentVariable("FASTBOOT_DEBUG") == "1";
        FastbootDebug.Output = message => Console.Error.WriteLine($"[DEBUG] {message}");

        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }

        var normalizedArgs = NormalizeFormatSyntax(args);

        if (normalizedArgs.Any(a => a is "-h" or "--help" or "help"))
        {
            ShowHelp();
            return;
        }

        if (normalizedArgs.Any(a => a is "--version" or "version"))
        {
            Console.Error.WriteLine("fastboot version 1.2.5");
            return;
        }

        var (globalSegment, commandSegments) = SplitIntoSegments(normalizedArgs);

        if (commandSegments.Count == 0)
        {
            ShowHelp();
            return;
        }

        if (commandSegments.Count == 1 && commandSegments[0][0] == "devices")
        {
            var deviceArgs = globalSegment.Concat(commandSegments[0]).ToArray();
            Parser.Default.ParseArguments<DevicesVerb>(deviceArgs)
                .WithParsed<DevicesVerb>(opts => ExecuteDeviceList(opts))
                .WithNotParsed(_ => { });
            return;
        }

        var standaloneSegments = new List<string[]>();
        var deviceSegments = new List<string[]>();

        foreach (var seg in commandSegments)
        {
            string cmd = seg[0];
            if (cmd is "connect" or "disconnect")
                standaloneSegments.Add(seg);
            else
                deviceSegments.Add(seg);
        }

        if (deviceSegments.Count > 0 && standaloneSegments.Count > 0)
        {
            Console.Error.WriteLine("fastboot: error: devices/connect/disconnect cannot be mixed with other commands");
            Environment.Exit(1);
            return;
        }

        foreach (var seg in standaloneSegments)
        {
            var cmdArgs = globalSegment.Concat(seg).ToArray();
            ExecuteStandaloneCommand(cmdArgs);
        }

        if (standaloneSegments.Count > 0) return;

        if (deviceSegments.Count == 0) return;

        ExecuteDeviceCommands(globalSegment, deviceSegments);
    }

    private static string[] NormalizeFormatSyntax(string[] args)
    {
        var result = new List<string>();
        foreach (var arg in args)
        {
            if (arg.StartsWith("format:", StringComparison.OrdinalIgnoreCase) && arg.Length > "format:".Length)
            {
                result.Add("format");
                result.Add(arg.Substring("format:".Length));
            }
            else if (arg.StartsWith("--set-active=", StringComparison.OrdinalIgnoreCase))
            {
                result.Add("--set-active");
                result.Add(arg.Substring("--set-active=".Length));
            }
            else
            {
                result.Add(arg);
            }
        }
        return result.ToArray();
    }

    private static (List<string> globalSegment, List<string[]> commandSegments) SplitIntoSegments(string[] args)
    {
        var globalSegment = new List<string>();
        var commandSegments = new List<string[]>();
        int i = 0;

        while (i < args.Length && !CommandTokens.Contains(args[i]) && args[i].StartsWith("-"))
        {
            string arg = args[i++];
            globalSegment.Add(arg);
            if ((arg == "-s" || arg == "--slot" || arg == "--set-active" || arg == "-a" ||
                 arg == "-S" || arg == "--fs-options" || arg == "--dtb" || arg == "--cmdline" ||
                 arg == "--header-version" || arg == "--base" || arg == "--page-size" ||
                 arg == "--kernel-offset" || arg == "--ramdisk-offset" || arg == "--second-offset" ||
                 arg == "--tags-offset" || arg == "--dtb-offset" || arg == "--os-version" ||
                 arg == "--os-patch-level")
                && i < args.Length && !args[i].StartsWith("-") && !CommandTokens.Contains(args[i]))
            {
                globalSegment.Add(args[i++]);
            }
        }

        while (i < args.Length)
        {
            var cmdSeg = new List<string>();
            while (i < args.Length && !CommandTokens.Contains(args[i]))
            {
                cmdSeg.Add(args[i++]);
            }
            if (i < args.Length)
            {
                cmdSeg.Add(args[i++]);
            }
            while (i < args.Length && !CommandTokens.Contains(args[i]))
            {
                cmdSeg.Add(args[i++]);
            }
            if (cmdSeg.Count > 0 && CommandTokens.Contains(cmdSeg[0]))
            {
                commandSegments.Add(cmdSeg.ToArray());
            }
        }

        return (globalSegment, commandSegments);
    }

    private static void ExecuteStandaloneCommand(string[] args)
    {
        Parser.Default.ParseArguments<ConnectVerb, DisconnectVerb>(args)
            .WithParsed<ConnectVerb>(opts => ExecuteConnect(opts))
            .WithParsed<DisconnectVerb>(opts => ExecuteDisconnect(opts))
            .WithNotParsed(errors =>
            {
                foreach (var err in errors)
                {
                    if (err.Tag == ErrorType.HelpRequestedError || err.Tag == ErrorType.HelpVerbRequestedError)
                        continue;
                    Console.Error.WriteLine("fastboot: error: " + err.Tag);
                }
                Environment.Exit(1);
            });
    }

    private static void ExecuteDeviceCommands(List<string> globalSegment, List<string[]> commandSegments)
    {
        bool convertSimgToRaw = !Environment.Is64BitOperatingSystem;
        long? sparseLimit = null;
        GlobalOptions? globals = null;

        var firstArgs = globalSegment.Concat(commandSegments[0]).ToArray();

        Parser.Default.ParseArguments(firstArgs,
            typeof(FlashVerb), typeof(FlashAllVerb), typeof(UpdateVerb), typeof(EraseVerb),
            typeof(FormatVerb), typeof(BootVerb), typeof(FetchVerb), typeof(GetVarVerb),
            typeof(RebootVerb), typeof(RebootBootloaderVerb), typeof(RebootFastbootVerb),
            typeof(RebootRecoveryVerb), typeof(FlashRawVerb), typeof(OemVerb), typeof(FlashingVerb),
            typeof(SetActiveVerb), typeof(CreateLogicalPartitionVerb), typeof(DeleteLogicalPartitionVerb),
            typeof(ResizeLogicalPartitionVerb), typeof(SnapshotUpdateVerb), typeof(ContinueVerb),
            typeof(StageVerb), typeof(GetStagedVerb), typeof(UploadVerb), typeof(GsiVerb),
            typeof(WipeSuperVerb), typeof(SignatureVerb), typeof(ShutdownVerb), typeof(SideloadVerb),
            typeof(StashVerb), typeof(UpdateSuperVerb)
        )
            .WithParsed<FlashVerb>(o => { globals = o; })
            .WithParsed<FlashAllVerb>(o => { globals = o; })
            .WithParsed<UpdateVerb>(o => { globals = o; })
            .WithParsed<EraseVerb>(o => { globals = o; })
            .WithParsed<FormatVerb>(o => { globals = o; })
            .WithParsed<BootVerb>(o => { globals = o; })
            .WithParsed<FetchVerb>(o => { globals = o; })
            .WithParsed<GetVarVerb>(o => { globals = o; })
            .WithParsed<RebootVerb>(o => { globals = o; })
            .WithParsed<RebootBootloaderVerb>(o => { globals = o; })
            .WithParsed<RebootFastbootVerb>(o => { globals = o; })
            .WithParsed<RebootRecoveryVerb>(o => { globals = o; })
            .WithParsed<FlashRawVerb>(o => { globals = o; })
            .WithParsed<OemVerb>(o => { globals = o; })
            .WithParsed<FlashingVerb>(o => { globals = o; })
            .WithParsed<SetActiveVerb>(o => { globals = o; })
            .WithParsed<CreateLogicalPartitionVerb>(o => { globals = o; })
            .WithParsed<DeleteLogicalPartitionVerb>(o => { globals = o; })
            .WithParsed<ResizeLogicalPartitionVerb>(o => { globals = o; })
            .WithParsed<SnapshotUpdateVerb>(o => { globals = o; })
            .WithParsed<ContinueVerb>(o => { globals = o; })
            .WithParsed<StageVerb>(o => { globals = o; })
            .WithParsed<GetStagedVerb>(o => { globals = o; })
            .WithParsed<UploadVerb>(o => { globals = o; })
            .WithParsed<GsiVerb>(o => { globals = o; })
            .WithParsed<WipeSuperVerb>(o => { globals = o; })
            .WithParsed<SignatureVerb>(o => { globals = o; })
            .WithParsed<ShutdownVerb>(o => { globals = o; })
            .WithParsed<SideloadVerb>(o => { globals = o; })
            .WithParsed<StashVerb>(o => { globals = o; })
            .WithParsed<UpdateSuperVerb>(o => { globals = o; })
            .WithNotParsed(errors =>
            {
                foreach (var err in errors)
                {
                    if (err.Tag is ErrorType.HelpRequestedError or ErrorType.HelpVerbRequestedError)
                        continue;
                    Console.Error.WriteLine("fastboot: error: " + (err is TokenError tokErr ? tokErr.Token : err.Tag.ToString()));
                }
                Environment.Exit(1);
            });
        if (globals == null) return;

        if (globals.Debug || globals.Verbose) FastbootDebug.IsEnabled = true;
        if (globals.Unbuffered) EnableUnbufferedOutput();
        if (globals.Fallback) UsbManager.ForceLibUsb = false;
        if (globals.ConvertSimgToRaw) convertSimgToRaw = true;

        if (!string.IsNullOrEmpty(globals.SparseSize))
            sparseLimit = ParseSize(globals.SparseSize);

        using FastbootDriver util = OpenTargetDriver(globals.Serial, globals.VendorId);
        util.ConvertSimgToRaw = convertSimgToRaw;

        if (sparseLimit.HasValue)
            FastbootDriver.SparseMaxDownloadSize = Math.Min((long)uint.MaxValue, sparseLimit.Value);

        WireUpDriverEvents(util);

        var totalTimer = System.Diagnostics.Stopwatch.StartNew();

        for (int segIdx = 0; segIdx < commandSegments.Count; segIdx++)
        {
            try
            {
                var seg = commandSegments[segIdx];
                var segArgs = segIdx == 0
                    ? firstArgs
                    : globalSegment.Concat(seg).ToArray();

                util.ResetTransport();
                ExecuteSingleCommand(util, segArgs, globals);
            }
            catch (Exception ex)
            {
                if (FastbootDebug.IsEnabled) Console.Error.WriteLine("[DEBUG] Exception: " + ex);
                Console.Error.WriteLine("fastboot: error: " + ex.Message);
                Environment.Exit(1);
            }
        }

        // AOSP: 所有命令完成后打印 "Finished. Total time: %.3fs"。
        Epilog(true); // 闭合可能残留的未决状态行（无则空操作）
        Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Finished. Total time: {0:F3}s", totalTimer.Elapsed.TotalSeconds));
    }

    private static void WireUpDriverEvents(FastbootDriver util)
    {
        // AOSP InfoMessage(): INFO 行打印 "(bootloader) %s"；TextMessage(): TEXT 原样输出。
        util.ReceivedFromDevice += (s, e) =>
        {
            if (e.NewInfo != null) Console.Error.WriteLine("(bootloader) " + e.NewInfo);
            if (e.NewText != null) Console.Error.Write(e.NewText);
        };

        // AOSP Status()/Epilog()：步骤开始打印 "%-50s " 前缀，命令完成后打印 OKAY/FAILED。
        util.CurrentStepChanged += (s, step) =>
        {
            if (string.IsNullOrEmpty(step)) return;
            if (IsPlainStep(step))
            {
                Epilog(true); // 闭合前一条未决状态行（若有），再直出普通行
                Console.Error.WriteLine(step);
                StatusPending = false;
            }
            else
            {
                Status(step);
            }
        };

        util.CommandCompleted += (s, e) =>
        {
            if (e.Quiet) return;
            var command = e.Command;
            var response = e.Response;

            if (response.Result == FastbootState.Fail || response.Result == FastbootState.Timeout)
            {
                string error = response.Result == FastbootState.Fail
                    ? $"remote: '{response.Response}'"
                    : "Status read timeout";
                // getvar 失败按 AOSP DisplayVarOrError 语义：Status("getvar:x") + "FAILED (...)"，
                // 不中断后续命令。
                if (command.StartsWith("getvar:", StringComparison.OrdinalIgnoreCase))
                {
                    string key = command.Substring("getvar:".Length);
                    Console.Error.Write(string.Format(CultureInfo.InvariantCulture, "{0,-50} ", "getvar:" + key));
                    Console.Error.WriteLine($"FAILED ({error})");
                    StatusPending = false;
                }
                else
                {
                    Epilog(false, error);
                    throw new Exception("Command failed");
                }
                return;
            }

            // 成功：getvar 按 AOSP DisplayVarOrError 输出 "label: value"（不打印 OKAY 状态行）；
            // 其他命令闭合 Status 前缀打印 "OKAY [%7.3fs]"。无步骤消息的命令（continue、
            // shutdown、get_staged 等）AOSP 的 epilog 同样打印 OKAY，这里补一个空 Status。
            if (command.StartsWith("getvar:", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(command, "getvar:all", StringComparison.OrdinalIgnoreCase))
            {
                string key = command.Substring("getvar:".Length);
                bool alreadyPrinted = response.Info.Any(x => x.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase));
                StatusPending = false;
                if (!alreadyPrinted)
                    Console.Error.WriteLine($"{key}: {response.Response}");
                return;
            }

            if (!StatusPending) Status(null);
            Epilog(true);
        };
    }

    private static void ExecuteSingleCommand(FastbootDriver util, string[] args, GlobalOptions globals)
    {
        string GetPartition(string baseName)
        {
            string? slot = globals.Slot;
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
                string other = current == "a" ? "b" : "a";
                return baseName + "_" + other;
            }

            if (util.HasSlot(baseName)) return baseName + "_" + slot;
            return baseName;
        }

        bool wipeUserdata = globals.Wipe;
        string? slot = globals.Slot;

        Parser.Default.ParseArguments(args,
            typeof(FlashVerb), typeof(FlashAllVerb), typeof(UpdateVerb), typeof(EraseVerb),
            typeof(FormatVerb), typeof(BootVerb), typeof(FetchVerb), typeof(GetVarVerb),
            typeof(RebootVerb), typeof(RebootBootloaderVerb), typeof(RebootFastbootVerb),
            typeof(RebootRecoveryVerb), typeof(FlashRawVerb), typeof(OemVerb), typeof(FlashingVerb),
            typeof(SetActiveVerb), typeof(CreateLogicalPartitionVerb), typeof(DeleteLogicalPartitionVerb),
            typeof(ResizeLogicalPartitionVerb), typeof(SnapshotUpdateVerb), typeof(ContinueVerb),
            typeof(StageVerb), typeof(GetStagedVerb), typeof(UploadVerb), typeof(GsiVerb),
            typeof(WipeSuperVerb), typeof(SignatureVerb), typeof(ShutdownVerb), typeof(SideloadVerb),
            typeof(StashVerb), typeof(UpdateSuperVerb)
        )
            .WithParsed<FlashVerb>(opts => ExecuteFlash(util, opts, globals, GetPartition))
            .WithParsed<FlashAllVerb>(opts => ExecuteFlashAll(util, opts, globals))
            .WithParsed<UpdateVerb>(opts => ExecuteUpdate(util, opts, globals))
            .WithParsed<EraseVerb>(opts =>
            {
                util.ErasePartition(GetPartition(opts.Partition)).ThrowIfError();
            })
            .WithParsed<FormatVerb>(opts => ExecuteFormat(util, opts, globals, GetPartition))
            .WithParsed<BootVerb>(opts => ExecuteBoot(util, opts, globals))
            .WithParsed<FetchVerb>(opts => ExecuteFetch(util, opts, GetPartition))
            .WithParsed<GetVarVerb>(opts =>
            {
                if (opts.Variable == "all") util.GetVarAll();
                else util.GetVar(opts.Variable);
            })
            .WithParsed<RebootVerb>(opts =>
            {
                string target = opts.Target ?? "";
                if (target == "fastboot")
                {
                    Console.Error.WriteLine("waiting for any device >");
                    util.EnsureUserspace();
                }
                else
                {
                    util.Reboot(target).ThrowIfError();
                }
            })
            .WithParsed<RebootBootloaderVerb>(opts => util.Reboot("bootloader").ThrowIfError())
            .WithParsed<RebootFastbootVerb>(opts =>
            {
                Console.Error.WriteLine("waiting for any device >");
                util.EnsureUserspace();
            })
            .WithParsed<RebootRecoveryVerb>(opts => util.Reboot("recovery").ThrowIfError())
            .WithParsed<FlashRawVerb>(opts => ExecuteFlashRaw(util, opts, globals, GetPartition))
            .WithParsed<OemVerb>(opts =>
            {
                string oemCmd = string.Join(" ", opts.CommandArgs);
                util.OemCommand(oemCmd).ThrowIfError();
            })
            .WithParsed<FlashingVerb>(opts =>
            {
                string flashCmd = string.Join(" ", opts.SubCommandArgs);
                util.FlashingCommand(flashCmd).ThrowIfError();
            })
            .WithParsed<SetActiveVerb>(opts =>
            {
                string? targetSlot = opts.ActiveSlot ?? slot;
                if (string.IsNullOrEmpty(targetSlot))
                {
                    string? current = util.GetVar("current-slot");
                    targetSlot = current == "a" ? "b" : "a";
                }
                util.SetActiveSlot(targetSlot).ThrowIfError();
            })
            .WithParsed<CreateLogicalPartitionVerb>(opts =>
            {
                if (!long.TryParse(opts.Size, out long sz))
                    throw new Exception("Invalid size: " + opts.Size);
                util.CreateLogicalPartition(opts.Partition, sz).ThrowIfError();
            })
            .WithParsed<DeleteLogicalPartitionVerb>(opts =>
            {
                util.DeleteLogicalPartition(opts.Partition).ThrowIfError();
            })
            .WithParsed<ResizeLogicalPartitionVerb>(opts =>
            {
                if (!long.TryParse(opts.Size, out long rsz))
                    throw new Exception("Invalid size: " + opts.Size);
                util.ResizeLogicalPartition(opts.Partition, rsz).ThrowIfError();
            })
            .WithParsed<SnapshotUpdateVerb>(opts =>
            {
                string action = opts.Action ?? "cancel";
                if (action is not ("cancel" or "merge"))
                    throw new Exception("usage: fastboot snapshot-update cancel|merge");
                util.SnapshotUpdate(action).ThrowIfError();
            })
            .WithParsed<ContinueVerb>(opts => util.Continue().ThrowIfError())
            .WithParsed<StageVerb>(opts =>
            {
                util.Stage(File.ReadAllBytes(opts.Filename)).ThrowIfError();
            })
            .WithParsed<GetStagedVerb>(opts =>
            {
                util.GetStaged(opts.OutFile);
            })
            .WithParsed<UploadVerb>(opts =>
            {
                util.Upload(opts.Name, opts.OutFile).ThrowIfError();
            })
            .WithParsed<GsiVerb>(opts =>
            {
                string sub = opts.SubCommand;
                if (sub is not ("wipe" or "disable" or "status"))
                    throw new Exception("usage: fastboot gsi wipe|disable|status");
                util.GsiCommand(sub).ThrowIfError();
            })
            .WithParsed<WipeSuperVerb>(opts =>
            {
                // AOSP wipe-super: with no explicit super_empty, look it up in $ANDROID_PRODUCT_OUT
                // (find_item_given_name("super_empty.img")); fall back to the raw protocol command
                // when the image is unavailable.
                string? empty = opts.SuperEmpty;
                if (string.IsNullOrEmpty(empty))
                {
                    string? productOut = Environment.GetEnvironmentVariable("ANDROID_PRODUCT_OUT");
                    if (!string.IsNullOrEmpty(productOut))
                    {
                        string candidate = Path.Combine(productOut, "super_empty.img");
                        if (File.Exists(candidate)) empty = candidate;
                    }
                }
                util.WipeSuper("super", empty).ThrowIfError();
            })
            .WithParsed<SignatureVerb>(opts =>
            {
                util.Signature(File.ReadAllBytes(opts.SignatureFile)).ThrowIfError();
            })
            .WithParsed<ShutdownVerb>(opts => util.Shutdown().ThrowIfError())
            .WithParsed<SideloadVerb>(opts => util.Sideload(opts.ZipPath).ThrowIfError())
            .WithParsed<StashVerb>(opts =>
            {
                util.Stash(opts.Name, opts.Filename).ThrowIfError();
            })
            .WithParsed<UpdateSuperVerb>(opts =>
            {
                util.UpdateSuper(opts.Partition, opts.MetadataPath, opts.Wipe).ThrowIfError();
            })
            .WithNotParsed(errors =>
            {
                foreach (var err in errors)
                {
                    if (err.Tag is ErrorType.HelpRequestedError or ErrorType.HelpVerbRequestedError)
                        continue;
                    throw new Exception(err.Tag.ToString());
                }
            });
    }

    private static void ExecuteFlash(FastbootDriver util, FlashVerb opts, GlobalOptions globals, Func<string, string> getPartition)
    {
        string? slot = globals.Slot;
        bool flashDisableVerity = globals.DisableVerity;
        bool flashDisableVerification = globals.DisableVerification;

        string flashPartition = opts.Partition ?? "";
        string flashFile;
        if (!string.IsNullOrEmpty(opts.Filename))
        {
            flashFile = opts.Filename;
        }
        else
        {
            string? productOut = Environment.GetEnvironmentVariable("ANDROID_PRODUCT_OUT");
            if (string.IsNullOrEmpty(productOut))
                throw new Exception("filename is required when ANDROID_PRODUCT_OUT is not set");
            flashFile = Path.Combine(productOut, flashPartition + ".img");
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
            string vbmetaTarget = getPartition(flashPartition);
            util.FlashVbmeta(vbmetaTarget, flashFile, flashDisableVerity, flashDisableVerification, globals.PrivateKeyPath).ThrowIfError();
        }
        else if (flashPartition.StartsWith("vendor_boot:ramdisk", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteVendorBootRamdiskFlash(util, flashFile, globals, slotOverride);
        }
        else
        {
            util.FlashImage(flashPartition, flashFile, slotOverride);
        }
    }

    private static void ExecuteVendorBootRamdiskFlash(FastbootDriver util, string ramdiskFile, GlobalOptions globals, string? slotOverride)
    {
        string vendorPartition = "vendor_boot";
        if (util.HasSlot(vendorPartition))
        {
            string current = util.GetCurrentSlot();
            if (!string.IsNullOrEmpty(current)) vendorPartition += "_" + current;
        }

        string tempOriginal = Path.Combine(Path.GetTempPath(), "vendor_boot_orig_" + Guid.NewGuid().ToString("N") + ".img");
        string tempRepacked = Path.Combine(Path.GetTempPath(), "vendor_boot_repacked_" + Guid.NewGuid().ToString("N") + ".img");
        try
        {
            util.Fetch(vendorPartition, tempOriginal).ThrowIfError();
            using (var originalStream = File.OpenRead(tempOriginal))
            {
                var vendorBoot = BootImage.Parse(originalStream);
                vendorBoot.Ramdisk = File.ReadAllBytes(ramdiskFile);
                if (!string.IsNullOrWhiteSpace(globals.Dtb) && File.Exists(globals.Dtb))
                    vendorBoot.Dtb = File.ReadAllBytes(globals.Dtb);

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

    private static void ExecuteFlashAll(FastbootDriver util, FlashAllVerb opts, GlobalOptions globals)
    {
        string? productOut = Environment.GetEnvironmentVariable("ANDROID_PRODUCT_OUT");
        if (string.IsNullOrEmpty(productOut))
            throw new Exception("ANDROID_PRODUCT_OUT not set. Please use: fastboot update ZIP");

        if (globals.Wipe)
        {
            Console.Error.WriteLine("Wiping userdata/cache as requested by -w...");
            util.ErasePartition("userdata");
            util.FormatPartition("userdata");
            util.ErasePartition("cache");
            util.FormatPartition("cache");
        }

        util.FlashAll(productOut, false, globals.SkipSecondary, globals.Force,
            !globals.DisableSuperOptimization, globals.DisableVerity, globals.DisableVerification,
            globals.DisableFastbootInfo, globals.ExcludeDynamicPartitions);

        if (!globals.SkipReboot) util.Reboot("");
    }

    private static void ExecuteUpdate(FastbootDriver util, UpdateVerb opts, GlobalOptions globals)
    {
        if (globals.Wipe)
        {
            Console.Error.WriteLine("Wiping userdata/cache as requested by -w...");
            util.ErasePartition("userdata");
            util.FormatPartition("userdata");
            util.ErasePartition("cache");
            util.FormatPartition("cache");
        }

        util.FlashUpdateZip(opts.ZipPath, globals.SkipSecondary, globals.DisableVerity,
            globals.DisableVerification, globals.Force, !globals.DisableSuperOptimization,
            globals.DisableFastbootInfo, globals.ExcludeDynamicPartitions);

        if (!globals.SkipReboot) util.Reboot("");
    }

    private static void ExecuteFormat(FastbootDriver util, FormatVerb opts, GlobalOptions globals, Func<string, string> getPartition)
    {
        string formatPartition;
        string? formatFsType = null;
        long? formatSize = null;

        if (opts.Partition2 != null)
        {
            string spec = opts.Partition ?? "";
            formatPartition = opts.Partition2;
            var parts = spec.Split(':', StringSplitOptions.None);
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) formatFsType = parts[0];
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) formatSize = ParseSize(parts[1]);
        }
        else
        {
            formatPartition = opts.Partition ?? "";
        }

        if (string.IsNullOrEmpty(formatPartition))
            throw new Exception("usage: fastboot format[:FS_TYPE[:SIZE]] <partition>");

        util.FormatPartition(getPartition(formatPartition), formatFsType, formatSize, globals.FsOptions).ThrowIfError();
    }

    private static void ExecuteBoot(FastbootDriver util, BootVerb opts, GlobalOptions globals)
    {
        var (dtb, cmdline, headerVersion, baseAddr, pageSize, kernelOffset,
             ramdiskOffset, secondOffset, tagsOffset, dtbOffset, osVersion) = ResolveBootOptions(globals);

        util.Boot(opts.Kernel, opts.Ramdisk, opts.Second, dtb, cmdline,
            headerVersion, baseAddr, pageSize, kernelOffset, ramdiskOffset,
            secondOffset, tagsOffset, dtbOffset, osVersion).ThrowIfError();
    }

    private static void ExecuteFlashRaw(FastbootDriver util, FlashRawVerb opts, GlobalOptions globals, Func<string, string> getPartition)
    {
        var (dtb, cmdline, headerVersion, baseAddr, pageSize, kernelOffset,
             ramdiskOffset, secondOffset, tagsOffset, dtbOffset, osVersion) = ResolveBootOptions(globals);

        util.FlashRaw(getPartition(opts.Partition), opts.Kernel, opts.Ramdisk, opts.Second,
            dtb, cmdline, headerVersion, baseAddr, pageSize, kernelOffset,
            ramdiskOffset, secondOffset, tagsOffset, dtbOffset, osVersion).ThrowIfError();
    }

    private static void ExecuteFetch(FastbootDriver util, FetchVerb opts, Func<string, string> getPartition)
    {
        string fetchPart = getPartition(opts.Partition);

        if (opts.Offset != null)
        {
            long offset = ParseSize(opts.Offset);
            long fetchSize = opts.Size != null ? ParseSize(opts.Size) : -1;
            util.Fetch(fetchPart, opts.OutFile, offset, fetchSize).ThrowIfError();
        }
        else
        {
            util.Fetch(fetchPart, opts.OutFile).ThrowIfError();
        }
    }

    private static (
        string? dtb, string? cmdline, uint headerVersion, uint baseAddr, uint pageSize,
        uint kernelOffset, uint ramdiskOffset, uint secondOffset, uint tagsOffset, uint dtbOffset, uint osVersion
    ) ResolveBootOptions(GlobalOptions globals)
    {
        string? dtb = globals.Dtb;
        string? cmdline = globals.Cmdline;
        uint headerVersion = ParseUIntOrDefault(globals.HeaderVersion, 0);
        uint baseAddr = ParseUIntOrDefault(globals.BaseAddr, 0x10000000);
        uint pageSize = ParseUIntOrDefault(globals.PageSize, 2048);
        uint kernelOffset = ParseUIntOrDefault(globals.KernelOffset, 0x00008000);
        uint ramdiskOffset = ParseUIntOrDefault(globals.RamdiskOffset, 0x01000000);
        uint secondOffset = ParseUIntOrDefault(globals.SecondOffset, 0x00F00000);
        uint tagsOffset = ParseUIntOrDefault(globals.TagsOffset, 0x00000100);
        uint dtbOffset = ParseUIntOrDefault(globals.DtbOffset, 0x01100000);
        uint osVersion = EncodeOsVersion(globals.OsVersion, globals.OsPatchLevel);

        return (dtb, cmdline, headerVersion, baseAddr, pageSize, kernelOffset,
                ramdiskOffset, secondOffset, tagsOffset, dtbOffset, osVersion);
    }

    private static uint ParseUIntOrDefault(string? value, uint defaultValue)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return ParseUIntOption("", value);
    }

    private static void ExecuteDeviceList(DevicesVerb opts)
    {
        bool verbose = opts.ListLong || (opts.Args.Any(a => a == "-l"));
        foreach (var dev in GetAllDevicesFiltered(opts.VendorId))
        {
            // Match official fastboot: "SERIAL\tfastboot\t<interface>" for -l,
            // where the third column is the device path (falling back to "fastboot").
            if (verbose)
            {
                string interfaceName = string.IsNullOrWhiteSpace(dev.DevicePath) ? "fastboot" : dev.DevicePath;
                Console.WriteLine($"{dev.SerialNumber}\tfastboot\t{interfaceName}");
            }
            else
            {
                Console.WriteLine($"{dev.SerialNumber}\tfastboot");
            }
            dev.Dispose();
        }

        foreach (var endpoint in LoadSavedNetworkTargets())
        {
            if (verbose) Console.WriteLine($"{endpoint}\tfastboot\t{endpoint}");
            else Console.WriteLine($"{endpoint}\tfastboot");
        }
    }

    private static void ExecuteConnect(ConnectVerb opts)
    {
        string endpoint = NormalizeNetworkEndpoint(opts.Endpoint);
        if (!TryOpenNetworkTransport(endpoint, out IFastbootTransport? transport, out string? error))
            throw new Exception(string.IsNullOrWhiteSpace(error) ? "failed to connect" : error);

        transport!.Dispose();

        var endpoints = LoadSavedNetworkTargets();
        if (!endpoints.Contains(endpoint, StringComparer.OrdinalIgnoreCase))
        {
            endpoints.Add(endpoint);
            SaveNetworkTargets(endpoints);
        }

        Console.Error.WriteLine("connected " + endpoint);
    }

    private static void ExecuteDisconnect(DisconnectVerb opts)
    {
        if (string.IsNullOrEmpty(opts.Endpoint))
        {
            SaveNetworkTargets(new List<string>());
            Console.Error.WriteLine("disconnected all network fastboot targets");
            return;
        }

        string endpoint = NormalizeNetworkEndpoint(opts.Endpoint);
        var endpoints = LoadSavedNetworkTargets();
        int removed = endpoints.RemoveAll(x => string.Equals(x, endpoint, StringComparison.OrdinalIgnoreCase));
        SaveNetworkTargets(endpoints);

        if (removed > 0) Console.Error.WriteLine("disconnected " + endpoint);
        else Console.Error.WriteLine("no such connection: " + endpoint);
    }

    private static FastbootDriver OpenTargetDriver(string? serial, string? vendorId)
    {
        if (!string.IsNullOrWhiteSpace(serial))
        {
            if (TryOpenNetworkTransport(serial, out IFastbootTransport? networkTransport, out string? networkError))
                return new FastbootDriver(networkTransport!);

            if (LooksLikeNetworkEndpoint(serial))
                throw new Exception(string.IsNullOrWhiteSpace(networkError) ? "failed to connect network fastboot target" : networkError);
        }

        var devices = GetAllDevicesFiltered(vendorId);
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
                if (!ReferenceEquals(dev, target)) dev.Dispose();
            return new FastbootDriver(target);
        }

        foreach (var dev in devices) dev.Dispose();

        if (serial == null)
        {
            if (TryOpenFirstSavedNetworkTarget(out IFastbootTransport? savedTransport))
                return new FastbootDriver(savedTransport!);

            Console.Error.WriteLine("< waiting for any device >");
            while (true)
            {
                System.Threading.Thread.Sleep(500);
                var waitedDevices = GetAllDevicesFiltered(vendorId);
                if (waitedDevices.Count > 0)
                {
                    var waitedTarget = waitedDevices[0];
                    for (int idx = 1; idx < waitedDevices.Count; idx++) waitedDevices[idx].Dispose();
                    return new FastbootDriver(waitedTarget);
                }
                foreach (var dev in waitedDevices) dev.Dispose();

                if (TryOpenFirstSavedNetworkTarget(out IFastbootTransport? waitTransport))
                    return new FastbootDriver(waitTransport!);
            }
        }

        throw new Exception("no devices/found");
    }

    /// <summary>
    /// Enumerates USB fastboot devices, optionally filtered by the -i vendor id option.
    /// <para>枚举 USB fastboot 设备，可依据 -i 厂商 ID 选项过滤。</para>
    /// </summary>
    private static List<UsbDevice> GetAllDevicesFiltered(string? vendorId)
    {
        var devices = UsbManager.GetAllDevices();
        if (string.IsNullOrWhiteSpace(vendorId)) return devices;
        ushort vid = ParseVendorId(vendorId);
        return devices.Where(d => d.VendorId == vid).ToList();
    }

    private static ushort ParseVendorId(string value)
    {
        string hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value.Substring(2) : value;
        if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort vid))
            return vid;

        if (ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort dec))
            return dec;

        throw new Exception($"invalid vendor id: {value} (expected hex like 0x2207)");
    }

    private static bool TryOpenFirstSavedNetworkTarget(out IFastbootTransport? transport)
    {
        foreach (var endpoint in LoadSavedNetworkTargets())
        {
            if (TryOpenNetworkTransport(endpoint, out transport, out _))
                return true;
        }
        transport = null;
        return false;
    }

    private static bool TryOpenNetworkTransport(string endpoint, out IFastbootTransport? transport, out string? error)
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

    private static bool LooksLikeNetworkEndpoint(string value)
        => value.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("udp:", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeNetworkEndpoint(string endpoint)
    {
        if (!TryParseNetworkEndpoint(endpoint, out string scheme, out string host, out int port, out string error))
            throw new Exception(error);
        return $"{scheme}:{host}:{port}";
    }

    private static bool TryParseNetworkEndpoint(string endpoint, out string scheme, out string host, out int port, out string error)
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

    private static string GetNetworkStorePath()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FirmwareKit.Comm.Fastboot.Cli");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "network_targets.txt");
    }

    private static List<string> LoadSavedNetworkTargets()
    {
        string path = GetNetworkStorePath();
        if (!File.Exists(path)) return new List<string>();

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadAllLines(path))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (TryParseNetworkEndpoint(trimmed, out string scheme, out string host, out int port, out _))
                set.Add($"{scheme}:{host}:{port}");
        }
        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void SaveNetworkTargets(List<string> endpoints)
    {
        string path = GetNetworkStorePath();
        var normalized = endpoints
            .Select(NormalizeNetworkEndpoint)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        File.WriteAllLines(path, normalized);
    }

    private static void EnableUnbufferedOutput()
    {
        var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(stdout);
        var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
        Console.SetError(stderr);
    }

    private static long ParseSize(string sizeStr)
    {
        long multiplier = 1;
        char last = char.ToLower(sizeStr[^1]);
        if (last == 'k') { multiplier = 1024; sizeStr = sizeStr[..^1]; }
        else if (last == 'm') { multiplier = 1024 * 1024; sizeStr = sizeStr[..^1]; }
        else if (last == 'g') { multiplier = 1024 * 1024 * 1024; sizeStr = sizeStr[..^1]; }
        return long.Parse(sizeStr, CultureInfo.InvariantCulture) * multiplier;
    }

    private static uint ParseUIntOption(string optionName, string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (uint.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
                return hex;
        }

        if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed))
            return parsed;

        throw new Exception($"invalid value for {optionName}: {value}");
    }

    private static uint EncodeOsVersion(string? osVersionText, string? osPatchLevelText)
    {
        int major = 0, minor = 0, patch = 0;

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

        int year = 2000, month = 0;
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

    private static void ShowHelp()
    {
        Console.Error.WriteLine("Usage: fastboot [-s <serial>] [-i <vendor-id>] [--slot <slot>] [-w] [-S <size>] [--skip-reboot] [--debug] <command> [args]");
        Console.Error.WriteLine("\noptions:");
        Console.Error.WriteLine("  -w                             Wipe userdata and cache after flashing.");
        Console.Error.WriteLine("  -s <serial>                    Specify device serial (USB or tcp:HOST[:PORT] / udp:HOST[:PORT]).");
        Console.Error.WriteLine("  -i <vendor-id>                 Specify a custom USB vendor id (hex, e.g. 0x2207 for Rockchip).");
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
        Console.Error.WriteLine("  --disable-verity               Disable dm-verity in vbmeta images. WARNING: Invalidates image signature!");
        Console.Error.WriteLine("  --disable-verification         Disable AVB verification in vbmeta images. WARNING: Invalidates image signature!");
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
        Console.Error.WriteLine("  --convert-simg-to-raw          Convert sparse image to raw image before flashing.");
        Console.Error.WriteLine("  --fallback                     Use platform native USB backend instead of libusb.");

        Console.Error.WriteLine("\nbasics:");
        Console.Error.WriteLine("  devices [-l]                   List connected devices.");
        Console.Error.WriteLine("  connect [tcp:|udp:]HOST[:PORT] Add and validate a network fastboot target.");
        Console.Error.WriteLine("  disconnect [tcp:|udp:]HOST[:PORT] Remove one or all saved network targets.");
        Console.Error.WriteLine("  getvar <name> | all            Display bootloader variable.");
        Console.Error.WriteLine("  reboot [bootloader|fastboot|recovery] Reboot device.");
        Console.Error.WriteLine("  continue                       Continue with autoboot.");

        Console.Error.WriteLine("\nnetwork fastboot (U-Boot / fastbootd over TCP or UDP):");
        Console.Error.WriteLine("  tcp:HOST[:PORT]                Connect over TCP (default port 5554).");
        Console.Error.WriteLine("                                 Supports U-Boot fastboot and userspace fastbootd.");
        Console.Error.WriteLine("  udp:HOST[:PORT]                Connect over UDP fastboot (default port 5554).");
        Console.Error.WriteLine("  -s tcp:HOST[:PORT]             Target a specific network device without connect.");
        Console.Error.WriteLine("  -s udp:HOST[:PORT]             Target a specific UDP fastboot device.");

        Console.Error.WriteLine("\nflashing:");
        Console.Error.WriteLine("  update <zip>                   Flash all partitions from a zip file.");
        Console.Error.WriteLine("  flashall                       Flash all partitions from $ANDROID_PRODUCT_OUT.");
        Console.Error.WriteLine("  flash <partition> [filename]   Write file to partition.");
        Console.Error.WriteLine("  flash [--disable-verity] [--disable-verification] vbmeta [filename]");
        Console.Error.WriteLine("  flash vendor_boot:ramdisk <ramdisk_file>");
        Console.Error.WriteLine("  flash:raw <p> <k> [r [s]]     Create and flash a raw boot image to a partition.");
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
        Console.Error.WriteLine("  boot <k> [r [s]]              Download and boot kernel from RAM.");

        Console.Error.WriteLine("\nAndroid Things / Miscellaneous:");
        Console.Error.WriteLine("  stage <filename>               Send file to device for next command.");
        Console.Error.WriteLine("  get_staged <outfile>           Write data staged by last command to file.");
        Console.Error.WriteLine("  upload <name> <outfile>        Legacy upload (e.g. last_kmsg).");
        Console.Error.WriteLine("  signature <file>               Send signature blob and install it.");
    }
}
