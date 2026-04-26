namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Locks critical partitions via flashing command.
    /// <para>通过 flashing 命令锁定关键分区。</para>
    /// </summary>
    public FastbootResponse FlashingLockCritical() => FlashingCommand("lock_critical");
}
