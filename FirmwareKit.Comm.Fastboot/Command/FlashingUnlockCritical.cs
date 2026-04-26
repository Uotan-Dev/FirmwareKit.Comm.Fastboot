namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Unlocks critical partitions via flashing command.
    /// <para>通过 flashing 命令解锁关键分区。</para>
    /// </summary>
    public FastbootResponse FlashingUnlockCritical() => FlashingCommand("unlock_critical");
}
