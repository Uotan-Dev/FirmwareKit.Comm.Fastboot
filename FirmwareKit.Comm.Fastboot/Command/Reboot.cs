

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    public FastbootResponse Reboot(string target = "")
    {
        FastbootDebug.Log($"Reboot(target={target})");
        if (string.IsNullOrEmpty(target))
        {
            NotifyCurrentStep("Rebooting");
            return RawCommand("reboot");
        }
        if (target == "recovery")
        {
            NotifyCurrentStep("Rebooting into recovery");
            return RawCommand("reboot-recovery");
        }
        if (target == "bootloader")
        {
            NotifyCurrentStep("Rebooting into bootloader");
            return RawCommand("reboot-bootloader");
        }
        if (target == "fastboot")
        {
            NotifyCurrentStep("Rebooting into fastboot");
            return RawCommand("reboot-fastboot");
        }
        NotifyCurrentStep($"Rebooting into {target}");
        return RawCommand("reboot-" + target);
    }


}






