namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Checks if the device bootloader is unlocked.
    /// <para>检查设备引导加载程序是否已解锁。</para>
    /// </summary>
    /// <returns>True if the device is unlocked, false otherwise. <para>如果设备已解锁则返回 true，否则返回 false。</para></returns>
    public bool IsUnlocked() => SafeGetVar("unlocked") == "yes";

    /// <summary>
    /// Gets the lock state of the device bootloader.
    /// <para>获取设备引导加载程序的锁定状态。</para>
    /// </summary>
    /// <returns>The lock state, or "unknown" if not available. <para>锁定状态，或如果不可用则返回 "unknown"。</para></returns>
    public string GetLockState() => SafeGetVar("locked", "unknown");

    /// <summary>
    /// Gets the device serial number.
    /// <para>获取设备序列号。</para>
    /// </summary>
    /// <returns>The serial number, or empty string if not available. <para>序列号，或如果不可用则返回空字符串。</para></returns>
    public string GetSerialNumber() => SafeGetVar("serialno");

    /// <summary>
    /// Gets the device version.
    /// <para>获取设备版本。</para>
    /// </summary>
    /// <returns>The version string, or empty string if not available. <para>版本字符串，或如果不可用则返回空字符串。</para></returns>
    public string GetVersion() => SafeGetVar("version");

    /// <summary>
    /// Gets the bootloader version.
    /// <para>获取引导加载程序版本。</para>
    /// </summary>
    /// <returns>The bootloader version, or empty string if not available. <para>引导加载程序版本，或如果不可用则返回空字符串。</para></returns>
    public string GetBootloaderVersion() => SafeGetVar("version-bootloader");

    /// <summary>
    /// Gets the baseband version.
    /// <para>获取基带版本。</para>
    /// </summary>
    /// <returns>The baseband version, or empty string if not available. <para>基带版本，或如果不可用则返回空字符串。</para></returns>
    public string GetBasebandVersion() => SafeGetVar("version-baseband");

    /// <summary>
    /// Gets the product name.
    /// <para>获取产品名称。</para>
    /// </summary>
    /// <returns>The product name, or empty string if not available. <para>产品名称，或如果不可用则返回空字符串。</para></returns>
    public string GetProduct() => SafeGetVar("product");

    /// <summary>
    /// Gets the current slot suffix (e.g., "_a" or "_b").
    /// <para>获取当前槽位后缀（如 "_a" 或 "_b"）。</para>
    /// </summary>
    /// <returns>The slot suffix, or empty string if not available. <para>槽位后缀，或如果不可用则返回空字符串。</para></returns>
    public string GetSlotSuffix() => SafeGetVar("slot-suffix");

    /// <summary>
    /// Gets the battery voltage.
    /// <para>获取电池电压。</para>
    /// </summary>
    /// <returns>The battery voltage, or empty string if not available. <para>电池电压，或如果不可用则返回空字符串。</para></returns>
    public string GetBatteryVoltage() => SafeGetVar("battery-voltage");

    /// <summary>
    /// Gets the battery state of charge (SoC).
    /// <para>获取电池充电状态 (SoC)。</para>
    /// </summary>
    /// <returns>The battery SoC, or empty string if not available. <para>电池 SoC，或如果不可用则返回空字符串。</para></returns>
    public string GetBatterySoC() => SafeGetVar("battery-soc");

    /// <summary>
    /// Gets the off-mode charge status.
    /// <para>获取关机充电状态。</para>
    /// </summary>
    /// <returns>The off-mode charge status, or empty string if not available. <para>关机充电状态，或如果不可用则返回空字符串。</para></returns>
    public string GetOffModeCharge() => SafeGetVar("off-mode-charge");

    /// <summary>
    /// Gets the snapshot update status.
    /// <para>获取快照更新状态。</para>
    /// </summary>
    /// <returns>The snapshot update status, or empty string if not available. <para>快照更新状态，或如果不可用则返回空字符串。</para></returns>
    public string GetSnapshotUpdateStatus() => SafeGetVar("snapshot-update-status");

    /// <summary>
    /// Gets the hardware revision.
    /// <para>获取硬件修订版本。</para>
    /// </summary>
    /// <returns>The hardware revision, or empty string if not available. <para>硬件修订版本，或如果不可用则返回空字符串。</para></returns>
    public string GetHardwareRevision() => SafeGetVar("hw-revision");

    private string SafeGetVar(string key, string defaultValue = "")
    {
        try { return GetVar(key); }
        catch { return defaultValue; }
    }
}
