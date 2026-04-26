namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Locks the device via flashing command.
    /// <para>通过 flashing 命令锁定设备。</para>
    /// </summary>
    public FastbootResponse FlashingLock() => FlashingCommand("lock");
}
