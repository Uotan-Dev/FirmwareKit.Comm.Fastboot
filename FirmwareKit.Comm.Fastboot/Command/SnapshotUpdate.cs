namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Performs a snapshot update action on the device. Common actions include "cancel" to abort an in-progress update.
    /// If the device requests a reboot to fastbootd to complete the action, this method will automatically reboot.
    /// <para>在设备上执行快照更新操作。常见操作包括 "cancel" 以中止正在进行的更新。
    /// 如果设备请求重启到 fastbootd 以完成操作，此方法将自动重启。</para>
    /// </summary>
    /// <param name="action">The snapshot action to perform (e.g., "cancel"). <para>要执行的快照操作（如 "cancel"）。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse SnapshotUpdate(string action = "cancel")
    {
        NotifyReceived(FastbootState.Text, $"Snapshot {action}");
        var res = RawCommand("snapshot-update:" + action);
        if (res.Response.IndexOf("reboot fastboot", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            NotifyReceived(FastbootState.Text, "Device requested reboot to fastbootd to finish snapshot action...");
            Reboot("fastboot");
        }
        return res;
    }
}
