namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Reboots the device. Supports targets: empty (normal), "recovery", "bootloader", "fastboot", or custom target.
    /// <para>重启设备。支持目标：空（正常）、"recovery"、"bootloader"、"fastboot" 或自定义目标。</para>
    /// </summary>
    public FastbootResponse Reboot(string target = "")
    {
        FastbootDebug.Log($"Reboot(target={target})");

        var (stepMsg, command) = string.IsNullOrEmpty(target) switch
        {
            true => ("Rebooting", "reboot"),
            false when target == "recovery" => ("Rebooting into recovery", "reboot-recovery"),
            false when target == "bootloader" => ("Rebooting into bootloader", "reboot-bootloader"),
            false when target == "fastboot" => ("Rebooting into fastboot", "reboot-fastboot"),
            false => ($"Rebooting into {target}", "reboot-" + target)
        };

        NotifyCurrentStep(stepMsg);
        return RawCommand(command);
    }
}
