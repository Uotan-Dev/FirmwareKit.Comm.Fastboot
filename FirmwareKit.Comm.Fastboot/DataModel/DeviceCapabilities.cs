namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// Represents the probed feature set of a connected fastboot device.
/// Non-Android / U-Boot based devices only implement a subset of the protocol, so
/// advanced flows (logical partitions, super optimization, userspace fastboot, CRC,
/// sparse chunking) must degrade gracefully based on these capabilities.
/// <para>表示已连接 fastboot 设备的探测特性集。
/// 非 Android / U-Boot 类设备只实现协议子集，因此高级流程（逻辑分区、super 优化、
/// 用户空间 fastboot、CRC、sparse 分块）必须依据这些能力信息优雅降级。</para>
/// </summary>
public sealed class DeviceCapabilities
{
    /// <summary>
    /// Gets or sets the fastboot protocol version reported by the device (e.g. "0.4").
    /// <para>获取或设置设备报告的 fastboot 协议版本（如 "0.4"）。</para>
    /// </summary>
    public string? ProtocolVersion { get; set; }

    /// <summary>
    /// Gets or sets the bootloader version reported by the device.
    /// <para>获取或设置设备报告的 bootloader 版本。</para>
    /// </summary>
    public string? BootloaderVersion { get; set; }

    /// <summary>
    /// Gets or sets the maximum download size in bytes supported by the device, or null when unknown.
    /// <para>获取或设置设备支持的最大下载大小（字节），未知时为 null。</para>
    /// </summary>
    public long? MaxDownloadSize { get; set; }

    /// <summary>
    /// Gets or sets whether the device is running userspace fastboot (fastbootd), or null when unsupported/unknown.
    /// <para>获取或设置设备是否运行用户空间 fastboot（fastbootd），不支持/未知时为 null。</para>
    /// </summary>
    public bool? IsUserspace { get; set; }

    /// <summary>
    /// Gets or sets whether the device supports CRC verification of downloads, or null when unsupported/unknown.
    /// <para>获取或设置设备是否支持下载 CRC 校验，不支持/未知时为 null。</para>
    /// </summary>
    public bool? HasCrc { get; set; }

    /// <summary>
    /// Gets or sets whether the device supports A/B slots (has-slot), or null when unsupported/unknown.
    /// <para>获取或设置设备是否支持 A/B 槽位（has-slot），不支持/未知时为 null。</para>
    /// </summary>
    public bool? SupportsSlots { get; set; }

    /// <summary>
    /// Gets or sets the number of slots (1 for non-A/B devices), or null when unknown.
    /// <para>获取或设置槽位数量（非 A/B 设备为 1），未知时为 null。</para>
    /// </summary>
    public int? SlotCount { get; set; }

    /// <summary>
    /// Gets or sets the current active slot suffix (without underscore), or null when unknown.
    /// <para>获取或设置当前活跃槽位后缀（不含下划线），未知时为 null。</para>
    /// </summary>
    public string? CurrentSlot { get; set; }

    /// <summary>
    /// Gets or sets the super partition name (e.g. "super"), or null when the device has no dynamic partitions.
    /// <para>获取或设置 super 分区名称（如 "super"），设备无动态分区时为 null。</para>
    /// </summary>
    public string? SuperPartitionName { get; set; }

    /// <summary>
    /// Gets or sets whether the device supports the is-logical variable (dynamic partition metadata), or null when unknown.
    /// <para>获取或设置设备是否支持 is-logical 变量（动态分区元数据），未知时为 null。</para>
    /// </summary>
    public bool? SupportsLogicalPartitions { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the probe was performed.
    /// <para>获取或设置探测执行时的 UTC 时间戳。</para>
    /// </summary>
    public DateTime ProbedAtUtc { get; set; }

    /// <summary>
    /// Gets a value indicating whether the probe was actually performed on a connected device.
    /// <para>获取一个值，指示是否已在连接的设备上实际执行探测。</para>
    /// </summary>
    public bool IsProbed { get; set; }
}
