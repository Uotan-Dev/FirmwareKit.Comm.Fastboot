using System;
using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        public FastbootResponse Reboot(string target = "system")
        {
            if (target == "recovery") return RawCommand("reboot-recovery");
            if (target == "bootloader") return RawCommand("reboot-bootloader");
            if (target == "fastboot") return RawCommand("reboot-fastboot");
            if (target == "system") return RawCommand("reboot");
            return RawCommand("reboot-" + target);
        }
    }
}
