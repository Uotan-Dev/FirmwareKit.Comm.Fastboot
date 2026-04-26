namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Unlocks the device bootloader via OEM command.
    /// <para>通过 OEM 命令解锁设备引导加载程序。</para>
    /// </summary>
    public FastbootResponse OemUnlock() => OemCommand("unlock");

    /// <summary>
    /// Locks the device bootloader via OEM command.
    /// <para>通过 OEM 命令锁定设备引导加载程序。</para>
    /// </summary>
    public FastbootResponse OemLock() => OemCommand("lock");

    /// <summary>
    /// Unlocks critical bootloader partitions via OEM command.
    /// <para>通过 OEM 命令解锁关键引导加载程序分区。</para>
    /// </summary>
    public FastbootResponse OemUnlockCritical() => OemCommand("unlock_critical");

    /// <summary>
    /// Locks critical bootloader partitions via OEM command.
    /// <para>通过 OEM 命令锁定关键引导加载程序分区。</para>
    /// </summary>
    public FastbootResponse OemLockCritical() => OemCommand("lock_critical");

    /// <summary>
    /// Queries device lock/unlock state via OEM command.
    /// <para>通过 OEM 命令查询设备锁定/解锁状态。</para>
    /// </summary>
    public FastbootResponse OemDeviceInfo() => OemCommand("device-info");
}
