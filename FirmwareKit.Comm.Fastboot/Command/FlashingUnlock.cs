namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Unlocks the device via flashing command.
    /// <para>通过 flashing 命令解锁设备。</para>
    /// </summary>
    public FastbootResponse FlashingUnlock() => FlashingCommand("unlock");
}
