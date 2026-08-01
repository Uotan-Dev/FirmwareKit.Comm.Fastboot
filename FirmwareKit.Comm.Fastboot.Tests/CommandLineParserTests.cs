using CommandLine;
using FirmwareKit.Comm.Fastboot.Cli.Options;

namespace FirmwareKit.Comm.Fastboot.Tests;

public class CommandLineParserTests
{
    private static T ParseSingle<T>(params string[] args) where T : class
    {
        var result = Parser.Default.ParseArguments(args, typeof(T));
        Assert.True(result.Tag == ParserResultType.Parsed, $"Failed to parse: {string.Join(" ", args)}");
        return Assert.IsType<T>(result.Value);
    }

    private static void AssertParseFails<T>(params string[] args) where T : class
    {
        var result = Parser.Default.ParseArguments(args, typeof(T));
        Assert.Equal(ParserResultType.NotParsed, result.Tag);
    }

    [Fact]
    public void Parse_DevicesVerb_NoOptions()
    {
        var opts = ParseSingle<DevicesVerb>("devices");
        Assert.False(opts.ListLong);
    }

    [Fact]
    public void Parse_DevicesVerb_WithListLongFlag()
    {
        var opts = ParseSingle<DevicesVerb>("devices", "-l");
        Assert.True(opts.ListLong);
    }

    [Fact]
    public void Parse_DevicesVerb_WithSerialGlobalOption()
    {
        var opts = ParseSingle<DevicesVerb>("devices", "-s", "device123");
        Assert.Equal("device123", opts.Serial);
        Assert.False(opts.ListLong);
    }

    [Fact]
    public void Parse_ConnectVerb_RequiredEndpoint()
    {
        var opts = ParseSingle<ConnectVerb>("connect", "tcp:192.168.1.1:5554");
        Assert.Equal("tcp:192.168.1.1:5554", opts.Endpoint);
    }

    [Fact]
    public void Parse_ConnectVerb_MissingEndpoint_Fails()
    {
        AssertParseFails<ConnectVerb>("connect");
    }

    [Fact]
    public void Parse_DisconnectVerb_AllTargets()
    {
        var opts = ParseSingle<DisconnectVerb>("disconnect");
        Assert.False(opts.Endpoint is string e && e.Length > 0);
    }

    [Fact]
    public void Parse_DisconnectVerb_SpecificTarget()
    {
        var opts = ParseSingle<DisconnectVerb>("disconnect", "tcp:192.168.1.1:5554");
        Assert.Equal("tcp:192.168.1.1:5554", opts.Endpoint);
    }

    [Fact]
    public void Parse_GetVarVerb_VariableName()
    {
        var opts = ParseSingle<GetVarVerb>("getvar", "version");
        Assert.Equal("version", opts.Variable);
    }

    [Fact]
    public void Parse_GetVarVerb_All()
    {
        var opts = ParseSingle<GetVarVerb>("getvar", "all");
        Assert.Equal("all", opts.Variable);
    }

    [Fact]
    public void Parse_GetVarVerb_MissingVariable_Fails()
    {
        AssertParseFails<GetVarVerb>("getvar");
    }

    [Fact]
    public void Parse_RebootVerb_NoTarget()
    {
        var opts = ParseSingle<RebootVerb>("reboot");
        Assert.False(opts.Target is string t && t.Length > 0);
    }

    [Fact]
    public void Parse_RebootVerb_WithBootloader()
    {
        var opts = ParseSingle<RebootVerb>("reboot", "bootloader");
        Assert.Equal("bootloader", opts.Target);
    }

    [Fact]
    public void Parse_RebootBootloaderVerb()
    {
        ParseSingle<RebootBootloaderVerb>("reboot-bootloader");
    }

    [Fact]
    public void Parse_RebootRecoveryVerb()
    {
        ParseSingle<RebootRecoveryVerb>("reboot-recovery");
    }

    [Fact]
    public void Parse_FlashVerb_PartitionOnly()
    {
        var opts = ParseSingle<FlashVerb>("flash", "boot");
        Assert.Equal("boot", opts.Partition);
        Assert.False(opts.Filename is string f && f.Length > 0);
    }

    [Fact]
    public void Parse_FlashVerb_PartitionAndFile()
    {
        var opts = ParseSingle<FlashVerb>("flash", "boot", "boot.img");
        Assert.Equal("boot", opts.Partition);
        Assert.Equal("boot.img", opts.Filename);
    }

    [Fact]
    public void Parse_FlashVerb_WithDisableVerity()
    {
        var opts = ParseSingle<FlashVerb>("flash", "--disable-verity", "vbmeta");
        Assert.True(opts.DisableVerity);
        Assert.Equal("vbmeta", opts.Partition);
    }

    [Fact]
    public void Parse_FlashVerb_WithDisableVerification()
    {
        var opts = ParseSingle<FlashVerb>("flash", "--disable-verification", "vbmeta", "vbmeta.img");
        Assert.True(opts.DisableVerification);
        Assert.Equal("vbmeta", opts.Partition);
        Assert.Equal("vbmeta.img", opts.Filename);
    }

    [Fact]
    public void Parse_FlashVerb_MissingPartition_Fails()
    {
        AssertParseFails<FlashVerb>("flash");
    }

    [Fact]
    public void Parse_FlashAllVerb_NoExtraArgs()
    {
        ParseSingle<FlashAllVerb>("flashall");
    }

    [Fact]
    public void Parse_FlashAllVerb_WithWipeFlag()
    {
        var opts = ParseSingle<FlashAllVerb>("flashall", "-w");
        Assert.True(opts.Wipe);
    }

    [Fact]
    public void Parse_UpdateVerb_WithZip()
    {
        var opts = ParseSingle<UpdateVerb>("update", "ota.zip");
        Assert.Equal("ota.zip", opts.ZipPath);
    }

    [Fact]
    public void Parse_UpdateVerb_MissingZip_Fails()
    {
        AssertParseFails<UpdateVerb>("update");
    }

    [Fact]
    public void Parse_FlashRawVerb_KernelOnly()
    {
        var opts = ParseSingle<FlashRawVerb>("flash:raw", "boot", "kernel.bin");
        Assert.Equal("boot", opts.Partition);
        Assert.Equal("kernel.bin", opts.Kernel);
        Assert.False(opts.Ramdisk is string r && r.Length > 0);
    }

    [Fact]
    public void Parse_FlashRawVerb_WithRamdisk()
    {
        var opts = ParseSingle<FlashRawVerb>("flash:raw", "recovery", "kernel.bin", "ramdisk.gz");
        Assert.Equal("recovery", opts.Partition);
        Assert.Equal("kernel.bin", opts.Kernel);
        Assert.Equal("ramdisk.gz", opts.Ramdisk);
        Assert.False(opts.Second is string s && s.Length > 0);
    }

    [Fact]
    public void Parse_FlashRawVerb_MissingKernel_Fails()
    {
        AssertParseFails<FlashRawVerb>("flash:raw", "boot");
    }

    [Fact]
    public void Parse_EraseVerb_Partition()
    {
        var opts = ParseSingle<EraseVerb>("erase", "boot");
        Assert.Equal("boot", opts.Partition);
    }

    [Fact]
    public void Parse_EraseVerb_MissingPartition_Fails()
    {
        AssertParseFails<EraseVerb>("erase");
    }

    [Fact]
    public void Parse_FormatVerb_PartitionOnly()
    {
        var opts = ParseSingle<FormatVerb>("format", "userdata");
        Assert.Equal("userdata", opts.Partition);
        Assert.Null(opts.Partition2);
    }

    [Fact]
    public void Parse_FormatVerb_MissingPartition_Fails()
    {
        AssertParseFails<FormatVerb>("format");
    }

    [Fact]
    public void Parse_FormatVerb_WithFsTypeViaNormalizedSyntax()
    {
        var opts = ParseSingle<FormatVerb>("format", "ext4:4096", "userdata");
        Assert.Equal("ext4:4096", opts.Partition);
        Assert.Equal("userdata", opts.Partition2);
    }

    [Fact]
    public void Parse_SetActiveVerb_ExplicitSlot()
    {
        var opts = ParseSingle<SetActiveVerb>("set_active", "a");
        Assert.Equal("a", opts.ActiveSlot);
    }

    [Fact]
    public void Parse_OemVerb_Command()
    {
        var opts = ParseSingle<OemVerb>("oem", "device-info");
        Assert.Contains("device-info", opts.CommandArgs);
    }

    [Fact]
    public void Parse_OemVerb_MissingCommand_Fails()
    {
        AssertParseFails<OemVerb>("oem");
    }

    [Fact]
    public void Parse_FlashingVerb_Lock()
    {
        var opts = ParseSingle<FlashingVerb>("flashing", "lock");
        Assert.Contains("lock", opts.SubCommandArgs);
    }

    [Fact]
    public void Parse_FlashingVerb_GetUnlockAbility()
    {
        var opts = ParseSingle<FlashingVerb>("flashing", "get_unlock_ability");
        Assert.Contains("get_unlock_ability", opts.SubCommandArgs);
    }

    [Fact]
    public void Parse_CreateLogicalPartitionVerb()
    {
        var opts = ParseSingle<CreateLogicalPartitionVerb>(
            "create-logical-partition", "vendor", "1073741824");
        Assert.Equal("vendor", opts.Partition);
        Assert.Equal("1073741824", opts.Size);
    }

    [Fact]
    public void Parse_DeleteLogicalPartitionVerb()
    {
        var opts = ParseSingle<DeleteLogicalPartitionVerb>("delete-logical-partition", "vendor");
        Assert.Equal("vendor", opts.Partition);
    }

    [Fact]
    public void Parse_ResizeLogicalPartitionVerb()
    {
        var opts = ParseSingle<ResizeLogicalPartitionVerb>(
            "resize-logical-partition", "vendor", "2147483648");
        Assert.Equal("vendor", opts.Partition);
        Assert.Equal("2147483648", opts.Size);
    }

    [Fact]
    public void Parse_SnapshotUpdateVerb_DefaultCancel()
    {
        var opts = ParseSingle<SnapshotUpdateVerb>("snapshot-update");
        Assert.Equal("cancel", opts.Action);
    }

    [Fact]
    public void Parse_SnapshotUpdateVerb_Merge()
    {
        var opts = ParseSingle<SnapshotUpdateVerb>("snapshot-update", "merge");
        Assert.Equal("merge", opts.Action);
    }

    [Fact]
    public void Parse_ContinueVerb()
    {
        ParseSingle<ContinueVerb>("continue");
    }

    [Fact]
    public void Parse_StageVerb()
    {
        var opts = ParseSingle<StageVerb>("stage", "file.img");
        Assert.Equal("file.img", opts.Filename);
    }

    [Fact]
    public void Parse_GetStagedVerb()
    {
        var opts = ParseSingle<GetStagedVerb>("get_staged", "output.bin");
        Assert.Equal("output.bin", opts.OutFile);
    }

    [Fact]
    public void Parse_UploadVerb()
    {
        var opts = ParseSingle<UploadVerb>("upload", "last_kmsg", "kmsg.txt");
        Assert.Equal("last_kmsg", opts.Name);
        Assert.Equal("kmsg.txt", opts.OutFile);
    }

    [Fact]
    public void Parse_GsiVerb_Wipe()
    {
        var opts = ParseSingle<GsiVerb>("gsi", "wipe");
        Assert.Equal("wipe", opts.SubCommand);
    }

    [Fact]
    public void Parse_WipeSuperVerb_NoSuperEmpty()
    {
        var opts = ParseSingle<WipeSuperVerb>("wipe-super");
        Assert.False(opts.SuperEmpty is string s && s.Length > 0);
    }

    [Fact]
    public void Parse_WipeSuperVerb_WithSuperEmpty()
    {
        var opts = ParseSingle<WipeSuperVerb>("wipe-super", "super_empty.img");
        Assert.Equal("super_empty.img", opts.SuperEmpty);
    }

    [Fact]
    public void Parse_SignatureVerb()
    {
        var opts = ParseSingle<SignatureVerb>("signature", "sig.bin");
        Assert.Equal("sig.bin", opts.SignatureFile);
    }

    [Fact]
    public void Parse_BootVerb_KernelOnly()
    {
        var opts = ParseSingle<BootVerb>("boot", "kernel.bin");
        Assert.Equal("kernel.bin", opts.Kernel);
        Assert.False(opts.Ramdisk is string r && r.Length > 0);
    }

    [Fact]
    public void Parse_BootVerb_WithRamdiskAndSecond()
    {
        var opts = ParseSingle<BootVerb>("boot", "kernel.bin", "ramdisk.gz", "second.bin");
        Assert.Equal("kernel.bin", opts.Kernel);
        Assert.Equal("ramdisk.gz", opts.Ramdisk);
        Assert.Equal("second.bin", opts.Second);
    }

    [Fact]
    public void Parse_FetchVerb_PartitionAndOutfile()
    {
        var opts = ParseSingle<FetchVerb>("fetch", "boot", "boot.img");
        Assert.Equal("boot", opts.Partition);
        Assert.Equal("boot.img", opts.OutFile);
        Assert.False(opts.Offset is string o && o.Length > 0);
        Assert.False(opts.Size is string sz && sz.Length > 0);
    }

    [Fact]
    public void Parse_FetchVerb_WithOffsetAndSize()
    {
        var opts = ParseSingle<FetchVerb>("fetch", "system", "system.img", "0", "1073741824");
        Assert.Equal("system", opts.Partition);
        Assert.Equal("system.img", opts.OutFile);
        Assert.Equal("0", opts.Offset);
        Assert.Equal("1073741824", opts.Size);
    }

    [Fact]
    public void Parse_SideloadVerb()
    {
        var opts = ParseSingle<SideloadVerb>("sideload", "ota.zip");
        Assert.Equal("ota.zip", opts.ZipPath);
    }

    [Fact]
    public void Parse_ShutdownVerb()
    {
        ParseSingle<ShutdownVerb>("shutdown");
    }

    [Fact]
    public void GlobalOptions_WipeFlag_OnFlashAll()
    {
        var opts = ParseSingle<FlashAllVerb>("flashall", "-w");
        Assert.True(opts.Wipe);
    }

    [Fact]
    public void GlobalOptions_SkipRebootFlag_OnFlashAll()
    {
        var opts = ParseSingle<FlashAllVerb>("flashall", "--skip-reboot");
        Assert.True(opts.SkipReboot);
    }

    [Fact]
    public void GlobalOptions_SlotOption_OnFlash()
    {
        var opts = ParseSingle<FlashVerb>("flash", "--slot", "a", "boot");
        Assert.Equal("a", opts.Slot);
    }

    [Fact]
    public void GlobalOptions_SerialOption_OnGetVar()
    {
        var opts = ParseSingle<GetVarVerb>("getvar", "-s", "192.168.1.1:5554", "version");
        Assert.Equal("192.168.1.1:5554", opts.Serial);
    }

    [Fact]
    public void GlobalOptions_SparseSizeOption_OnFlashAll()
    {
        var opts = ParseSingle<FlashAllVerb>("flashall", "-S", "256m");
        Assert.Equal("256m", opts.SparseSize);
    }

    [Fact]
    public void GlobalOptions_Debug_OnReboot()
    {
        var opts = ParseSingle<RebootVerb>("reboot", "--debug");
        Assert.True(opts.Debug);
    }

    [Fact]
    public void GlobalOptions_ForceFlag_OnUpdate()
    {
        var opts = ParseSingle<UpdateVerb>("update", "--force", "ota.zip");
        Assert.True(opts.Force);
    }

    [Fact]
    public void GlobalOptions_SkipSecondary_OnFlashAll()
    {
        var opts = ParseSingle<FlashAllVerb>("flashall", "--skip-secondary");
        Assert.True(opts.SkipSecondary);
    }

    [Fact]
    public void GlobalOptions_DisableVerity_OnFlashAll()
    {
        var opts = ParseSingle<FlashAllVerb>("flashall", "--disable-verity");
        Assert.True(opts.DisableVerity);
    }

    [Fact]
    public void GlobalOptions_DisableVerification_OnFlashAll()
    {
        var opts = ParseSingle<FlashAllVerb>("flashall", "--disable-verification");
        Assert.True(opts.DisableVerification);
    }

    [Fact]
    public void BootImageOptions_Base_OnBoot()
    {
        var opts = ParseSingle<BootVerb>("boot", "--base", "0x10000000", "kernel.bin");
        Assert.Equal("0x10000000", opts.BaseAddr);
    }

    [Fact]
    public void BootImageOptions_PageSize_OnFlashRaw()
    {
        var opts = ParseSingle<FlashRawVerb>("flash:raw", "--page-size", "4096", "boot", "kernel.bin");
        Assert.Equal("4096", opts.PageSize);
    }

    [Fact]
    public void BootImageOptions_KernelOffset_OnBoot()
    {
        var opts = ParseSingle<BootVerb>("boot", "--kernel-offset", "0x8000", "kernel.bin");
        Assert.Equal("0x8000", opts.KernelOffset);
    }

    [Fact]
    public void GlobalOptions_FsOptions_OnFormat()
    {
        var opts = ParseSingle<FormatVerb>("format", "--fs-options", "casefold", "userdata");
        Assert.Equal("casefold", opts.FsOptions);
    }

    [Fact]
    public void GlobalOptions_Dtb_OnBoot()
    {
        var opts = ParseSingle<BootVerb>("boot", "--dtb", "dtb.img", "kernel.bin");
        Assert.Equal("dtb.img", opts.Dtb);
    }

    [Fact]
    public void GlobalOptions_Cmdline_OnBoot()
    {
        var opts = ParseSingle<BootVerb>("boot", "--cmdline", "androidboot.hardware=qcom", "kernel.bin");
        Assert.Equal("androidboot.hardware=qcom", opts.Cmdline);
    }

    [Fact]
    public void UnknownVerb_ReturnsNotParsed()
    {
        var result = Parser.Default.ParseArguments(["unknown", "command"], typeof(FlashVerb));
        Assert.Equal(ParserResultType.NotParsed, result.Tag);
    }

    [Fact]
    public void VerbNotMatched_ReturnsNotParsed()
    {
        var types = new[] { typeof(FlashVerb), typeof(EraseVerb) };
        var result = Parser.Default.ParseArguments(["unknowncommand", "arg"], types);
        Assert.Equal(ParserResultType.NotParsed, result.Tag);
    }

    [Fact]
    public void Parse_UpdateSuperVerb()
    {
        var opts = ParseSingle<UpdateSuperVerb>("update-super", "super", "super_empty.img", "--wipe");
        Assert.Equal("super", opts.Partition);
        Assert.Equal("super_empty.img", opts.MetadataPath);
        Assert.True(opts.Wipe);
    }

    [Fact]
    public void Parse_StashVerb()
    {
        var opts = ParseSingle<StashVerb>("stash", "my_data", "data.bin");
        Assert.Equal("my_data", opts.Name);
        Assert.Equal("data.bin", opts.Filename);
    }
}
