

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Snapshot update operation (Virtual A/B)
    /// </summary>
    public FastbootResponse SnapshotUpdate(string action = "cancel")
    {
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






