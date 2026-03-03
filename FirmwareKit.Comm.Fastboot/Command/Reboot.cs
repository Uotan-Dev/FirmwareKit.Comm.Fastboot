using System;
using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        public FastbootResponse Reboot(string target = "")
        {
            if (string.IsNullOrEmpty(target)) return RawCommand("reboot");
            if (target == "recovery") return RawCommand("reboot-recovery");
            if (target == "bootloader") return RawCommand("reboot-bootloader");
            if (target == "fastboot") return RawCommand("reboot-fastboot");
            return RawCommand("reboot-" + target);
        }
    }
}
