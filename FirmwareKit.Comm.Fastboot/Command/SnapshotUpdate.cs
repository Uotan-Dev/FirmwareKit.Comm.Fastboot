using System;
using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Snapshot update operation (Virtual A/B)
        /// </summary>
        public FastbootResponse SnapshotUpdate(string action = "cancel")
        {
            if (action != "cancel" && action != "merge")
                throw new ArgumentException("SnapshotUpdate action must be 'cancel' or 'merge'");
            var res = RawCommand("snapshot-update:" + action);
            if (res.Response.Contains("reboot fastboot", StringComparison.OrdinalIgnoreCase))
            {
                NotifyCurrentStep("Device requested reboot to fastbootd to finish snapshot action...");
                Reboot("fastboot");
            }
            return res;
        }
    }
}
