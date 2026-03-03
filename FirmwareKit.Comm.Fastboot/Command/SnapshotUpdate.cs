using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Snapshot update operation (Virtual A/B)
    /// </summary>
    public FastbootResponse SnapshotUpdate(string action = "cancel")
    {
        // AOSP SnapshotUpdateCommand: prolog_("Snapshot %s", command.c_str()); RawCommand(FB_CMD_SNAPSHOT_UPDATE ":" + command, ...);
        NotifyReceived(FastbootState.Text, $"Snapshot {action}");
        var res = RawCommand("snapshot-update:" + action);
        if (res.Response.Contains("reboot fastboot", StringComparison.OrdinalIgnoreCase))
        {
            NotifyReceived(FastbootState.Text, "Device requested reboot to fastbootd to finish snapshot action...");
            Reboot("fastboot");
        }
        return res;
    }


}