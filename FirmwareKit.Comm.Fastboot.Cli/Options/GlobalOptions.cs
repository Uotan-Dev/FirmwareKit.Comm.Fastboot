using CommandLine;

namespace FirmwareKit.Comm.Fastboot.Cli.Options;

public class GlobalOptions
{
    [Option('s', "serial", HelpText = "Specify device serial number (USB or tcp:HOST[:PORT] / udp:HOST[:PORT]).")]
    public string? Serial { get; set; }

    [Option('i', "vendor-id", HelpText = "Specify a custom USB vendor id (hex, e.g. 0x2207 for Rockchip).")]
    public string? VendorId { get; set; }

    [Option("slot", HelpText = "Specify active slot (a/b/all/other).")]
    public string? Slot { get; set; }

    [Option('a', "set-active", HelpText = "Sets the active slot. If no slot is provided, it will set the inactive slot to active.")]
    public string? SetActiveSlot { get; set; }

    [Option('w', HelpText = "Wipe userdata and cache after flashing.")]
    public bool Wipe { get; set; }

    [Option('S', "sparse-size", HelpText = "Break into sparse files no larger than SIZE (supports k/m/g suffix).")]
    public string? SparseSize { get; set; }

    [Option("skip-reboot", HelpText = "Don't reboot device after flashing all.")]
    public bool SkipReboot { get; set; }

    [Option("skip-secondary", HelpText = "Don't flash secondary slots in flashall/update.")]
    public bool SkipSecondary { get; set; }

    [Option("force", HelpText = "Ignore compatibility checks for flashall/update.")]
    public bool Force { get; set; }

    [Option("disable-super-optimization", HelpText = "Disable optimized super-partition flashing.")]
    public bool DisableSuperOptimization { get; set; }

    [Option("exclude-dynamic-partitions", HelpText = "Skip flashing logical dynamic partitions.")]
    public bool ExcludeDynamicPartitions { get; set; }

    [Option("disable-fastboot-info", HelpText = "Ignore fastboot-info.txt and use image scan fallback.")]
    public bool DisableFastbootInfo { get; set; }

    [Option("disable-verity", HelpText = "Disable dm-verity in vbmeta images. WARNING: Invalidates image signature!")]
    public bool DisableVerity { get; set; }

    [Option("disable-verification", HelpText = "Disable AVB verification in vbmeta images. WARNING: Invalidates image signature!")]
    public bool DisableVerification { get; set; }

    [Option("private-key", HelpText = "Path to RSA private key (PKCS8 format) for re-signing vbmeta images after modification.")]
    public string? PrivateKeyPath { get; set; }

    [Option("fs-options", HelpText = "File system options for format (e.g. casefold).")]
    public string? FsOptions { get; set; }

    [Option("dtb", HelpText = "Default DTB file for boot/flash:raw.")]
    public string? Dtb { get; set; }

    [Option("cmdline", HelpText = "Default kernel cmdline for boot/flash:raw.")]
    public string? Cmdline { get; set; }

    [Option("base", HelpText = "Default base address for boot/flash:raw.")]
    public string? BaseAddr { get; set; }

    [Option("page-size", HelpText = "Default page size for boot/flash:raw.")]
    public string? PageSize { get; set; }

    [Option("header-version", HelpText = "Default boot image header version for boot/flash:raw.")]
    public string? HeaderVersion { get; set; }

    [Option("kernel-offset", HelpText = "Kernel offset for boot/flash:raw image header.")]
    public string? KernelOffset { get; set; }

    [Option("ramdisk-offset", HelpText = "Ramdisk offset for boot/flash:raw image header.")]
    public string? RamdiskOffset { get; set; }

    [Option("second-offset", HelpText = "Second stage offset for boot/flash:raw image header.")]
    public string? SecondOffset { get; set; }

    [Option("tags-offset", HelpText = "Tags offset for boot/flash:raw image header.")]
    public string? TagsOffset { get; set; }

    [Option("dtb-offset", HelpText = "DTB offset for boot/flash:raw image header.")]
    public string? DtbOffset { get; set; }

    [Option("os-version", HelpText = "Android OS version for boot image header (e.g. 14.0.0).")]
    public string? OsVersion { get; set; }

    [Option("os-patch-level", HelpText = "Android OS patch level for boot image header (e.g. 2024-12).")]
    public string? OsPatchLevel { get; set; }

    [Option("debug", HelpText = "Verbose debug logging output.")]
    public bool Debug { get; set; }

    [Option('v', "verbose", HelpText = "Verbose logging output.")]
    public bool Verbose { get; set; }

    [Option("unbuffered", HelpText = "Disable stdio buffering.")]
    public bool Unbuffered { get; set; }

    [Option("convert-simg-to-raw", HelpText = "Convert sparse image to raw image before flashing.")]
    public bool ConvertSimgToRaw { get; set; }

    [Option("fallback", HelpText = "Use platform native USB backend instead of libusb.")]
    public bool Fallback { get; set; }

    [Option("version", HelpText = "Display version information.")]
    public bool ShowVersion { get; set; }
}
