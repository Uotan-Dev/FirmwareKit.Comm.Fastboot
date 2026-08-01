using CommandLine;

namespace FirmwareKit.Comm.Fastboot.Cli.Options;

[Verb("devices", HelpText = "List connected devices.")]
public class DevicesVerb : GlobalOptions
{
    [Option('l', "list", HelpText = "Verbose listing with transport type.")]
    public bool ListLong { get; set; }

    [Value(0, HelpText = "Additional args (e.g. -l).")]
    public IEnumerable<string> Args { get; set; } = [];
}

[Verb("connect", HelpText = "Add and validate a network fastboot target.")]
public class ConnectVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "HOST[:PORT]", HelpText = "Network endpoint in the form [tcp:|udp:]HOST[:PORT].")]
    public string Endpoint { get; set; } = "";
}

[Verb("disconnect", HelpText = "Remove one or all saved network targets.")]
public class DisconnectVerb : GlobalOptions
{
    [Value(1, Required = false, MetaName = "HOST[:PORT]", HelpText = "Network endpoint to remove, or omit to disconnect all.")]
    public string? Endpoint { get; set; }
}

[Verb("getvar", HelpText = "Display bootloader variable.")]
public class GetVarVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "variable|all", HelpText = "Variable name to query, or 'all' for all variables.")]
    public string Variable { get; set; } = "";
}

[Verb("reboot", HelpText = "Reboot device.")]
public class RebootVerb : GlobalOptions
{
    [Value(1, Required = false, MetaName = "target", HelpText = "Reboot target: (none), bootloader, fastboot, or recovery.")]
    public string? Target { get; set; }
}

[Verb("reboot-bootloader", HelpText = "Reboot device into bootloader.")]
public class RebootBootloaderVerb : GlobalOptions
{
}

[Verb("reboot-fastboot", HelpText = "Reboot device into fastboot mode (fastbootd).")]
public class RebootFastbootVerb : GlobalOptions
{
}

[Verb("reboot-recovery", HelpText = "Reboot device into recovery mode.")]
public class RebootRecoveryVerb : GlobalOptions
{
}

[Verb("flash", HelpText = "Write a file to a partition.")]
public class FlashVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "partition", HelpText = "Target partition to flash.")]
    public string Partition { get; set; } = "";

    [Value(2, Required = false, MetaName = "filename", HelpText = "Image file to flash. Uses $ANDROID_PRODUCT_OUT/{partition}.img if omitted.")]
    public string? Filename { get; set; }
}

[Verb("flashall", HelpText = "Flash all partitions from $ANDROID_PRODUCT_OUT.")]
public class FlashAllVerb : GlobalOptions
{
}

[Verb("update", HelpText = "Flash all partitions from a zip file.")]
public class UpdateVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "zip", HelpText = "Path to the update.zip file.")]
    public string ZipPath { get; set; } = "";
}

[Verb("flash:raw", HelpText = "Create and flash a raw boot image to a partition.")]
public class FlashRawVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "partition", HelpText = "Target partition (e.g., boot, recovery).")]
    public string Partition { get; set; } = "";

    [Value(2, Required = true, MetaName = "kernel", HelpText = "Path to kernel image file.")]
    public string Kernel { get; set; } = "";

    [Value(3, Required = false, MetaName = "ramdisk", HelpText = "Path to ramdisk image file.")]
    public string? Ramdisk { get; set; }

    [Value(4, Required = false, MetaName = "second", HelpText = "Path to second stage loader image.")]
    public string? Second { get; set; }
}

[Verb("erase", HelpText = "Erase a flash partition.")]
public class EraseVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "partition", HelpText = "Partition to erase.")]
    public string Partition { get; set; } = "";
}

[Verb("format", HelpText = "Format a flash partition. Supports inline format:FS_TYPE[:SIZE] syntax.")]
public class FormatVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "partition", HelpText = "Partition to format. Precede with optional FS_TYPE[:SIZE] e.g. ext4:4096.")]
    public string Partition { get; set; } = "";

    [Value(2, Required = false, MetaName = "partition2", HelpText = "Partition when FS_TYPE[:SIZE] is provided as first arg.")]
    public string? Partition2 { get; set; }
}

[Verb("set_active", HelpText = "Set the active A/B slot.")]
public class SetActiveVerb : GlobalOptions
{
    [Value(1, Required = false, MetaName = "slot", HelpText = "Slot to set as active (a/b). Uses alternate slot if omitted.")]
    public string? ActiveSlot { get; set; }
}

[Verb("oem", HelpText = "Execute an OEM-specific command.")]
public class OemVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "command", HelpText = "OEM command and its arguments.")]
    public IEnumerable<string> CommandArgs { get; set; } = [];
}

[Verb("flashing", HelpText = "Execute a flashing sub-command (lock/unlock/get_unlock_ability).")]
public class FlashingVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "subcommand", HelpText = "Flashing sub-command: lock, unlock, lock_critical, unlock_critical, or get_unlock_ability.")]
    public IEnumerable<string> SubCommandArgs { get; set; } = [];
}

[Verb("create-logical-partition", HelpText = "Create a logical partition of given size.")]
public class CreateLogicalPartitionVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "partition", HelpText = "Name of the logical partition.")]
    public string Partition { get; set; } = "";

    [Value(2, Required = true, MetaName = "size", HelpText = "Size in bytes.")]
    public string Size { get; set; } = "";
}

[Verb("delete-logical-partition", HelpText = "Delete a logical partition.")]
public class DeleteLogicalPartitionVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "partition", HelpText = "Name of the logical partition to delete.")]
    public string Partition { get; set; } = "";
}

[Verb("resize-logical-partition", HelpText = "Resize a logical partition.")]
public class ResizeLogicalPartitionVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "partition", HelpText = "Name of the logical partition.")]
    public string Partition { get; set; } = "";

    [Value(2, Required = true, MetaName = "size", HelpText = "New size in bytes.")]
    public string Size { get; set; } = "";
}

[Verb("snapshot-update", HelpText = "Manage snapshot updates.")]
public class SnapshotUpdateVerb : GlobalOptions
{
    [Value(1, Required = false, MetaName = "action", HelpText = "Action: cancel (default) or merge.")]
    public string Action { get; set; } = "cancel";
}

[Verb("continue", HelpText = "Continue with autoboot.")]
public class ContinueVerb : GlobalOptions
{
}

[Verb("stage", HelpText = "Send file to device for next command.")]
public class StageVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "filename", HelpText = "File to stage on the device.")]
    public string Filename { get; set; } = "";
}

[Verb("get_staged", HelpText = "Write data staged by last command to a file.")]
public class GetStagedVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "outfile", HelpText = "Output file path for the staged data.")]
    public string OutFile { get; set; } = "";
}

[Verb("upload", HelpText = "Legacy upload (e.g. last_kmsg).")]
public class UploadVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "name", HelpText = "Name of the data to upload (e.g., last_kmsg).")]
    public string Name { get; set; } = "";

    [Value(2, Required = true, MetaName = "outfile", HelpText = "Output file path.")]
    public string OutFile { get; set; } = "";
}

[Verb("gsi", HelpText = "Manage GSI installation.")]
public class GsiVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "subcommand", HelpText = "GSI action: wipe, disable, or status.")]
    public string SubCommand { get; set; } = "";
}

[Verb("wipe-super", HelpText = "Wipe the super partition.")]
public class WipeSuperVerb : GlobalOptions
{
    [Value(1, Required = false, MetaName = "super_empty", HelpText = "Path to super_empty.img for optimized wipe.")]
    public string? SuperEmpty { get; set; }
}

[Verb("signature", HelpText = "Send a signature blob and install it.")]
public class SignatureVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "file", HelpText = "Path to the signature file.")]
    public string SignatureFile { get; set; } = "";
}

[Verb("boot", HelpText = "Download and boot kernel from RAM.")]
public class BootVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "kernel", HelpText = "Path to kernel image.")]
    public string Kernel { get; set; } = "";

    [Value(2, Required = false, MetaName = "ramdisk", HelpText = "Path to ramdisk image.")]
    public string? Ramdisk { get; set; }

    [Value(3, Required = false, MetaName = "second", HelpText = "Path to second stage loader.")]
    public string? Second { get; set; }
}

[Verb("fetch", HelpText = "Fetch a partition image from the device.")]
public class FetchVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "partition", HelpText = "Partition to fetch.")]
    public string Partition { get; set; } = "";

    [Value(2, Required = true, MetaName = "outfile", HelpText = "Output file path.")]
    public string OutFile { get; set; } = "";

    [Value(3, Required = false, MetaName = "offset", HelpText = "Byte offset within the partition.")]
    public string? Offset { get; set; }

    [Value(4, Required = false, MetaName = "size", HelpText = "Number of bytes to fetch.")]
    public string? Size { get; set; }
}

[Verb("sideload", HelpText = "Sideload an OTA package.", Hidden = true)]
public class SideloadVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "zip", HelpText = "Path to the OTA zip file.")]
    public string ZipPath { get; set; } = "";
}

[Verb("shutdown", HelpText = "Shutdown the device.", Hidden = true)]
public class ShutdownVerb : GlobalOptions
{
}

[Verb("stash", HelpText = "Stash data on the device.", Hidden = true)]
public class StashVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "name", HelpText = "Name to stash under.")]
    public string Name { get; set; } = "";

    [Value(2, Required = true, MetaName = "filename", HelpText = "File to stash.")]
    public string Filename { get; set; } = "";
}

[Verb("update-super", HelpText = "Update super partition metadata.", Hidden = true)]
public class UpdateSuperVerb : GlobalOptions
{
    [Value(1, Required = true, MetaName = "partition", HelpText = "Target super partition.")]
    public string Partition { get; set; } = "super";

    [Value(2, Required = true, MetaName = "metadata", HelpText = "Path to super_empty.img or metadata file.")]
    public string MetadataPath { get; set; } = "";

    [Option("wipe", HelpText = "Wipe before updating.")]
    public new bool Wipe { get; set; }
}
