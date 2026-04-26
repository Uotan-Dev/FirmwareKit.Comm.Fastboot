namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Formats the specified partition, automatically using the current active slot if the partition has slots.
    /// <para>格式化指定分区，如果分区有槽位，则自动使用当前活跃槽位。</para>
    /// </summary>
    /// <param name="partition">The partition to format. <para>要格式化的分区。</para></param>
    /// <param name="fsType">The filesystem type (optional). <para>文件系统类型（可选）。</para></param>
    /// <param name="size">The partition size in bytes (optional). <para>分区大小（字节，可选）。</para></param>
    /// <param name="options">Additional formatting options (optional). <para>额外的格式化选项（可选）。</para></param>
    /// <returns>A FastbootResponse indicating the result of the operation. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse FormatPartition(string partition, string? fsType = null, long? size = null, string? options = null)
    {
        string targetPartition = partition;
        if (HasSlot(partition))
        {
            targetPartition = partition + "_" + GetCurrentSlot();
        }

        return FormatPartitionInternal(targetPartition, fsType, size, options);
    }

    /// <summary>
    /// Formats the specified partition with the given slot suffix or current slot if not specified.
    /// <para>使用指定的槽位后缀或当前槽位（如果未指定）格式化分区。</para>
    /// </summary>
    /// <param name="partition">The partition to format. <para>要格式化的分区。</para></param>
    /// <param name="slotOverride">The slot suffix to use (e.g., "a" or "b"), or null to use current slot. <para>要使用的槽位后缀（如 "a" 或 "b"），或 null 使用当前槽位。</para></param>
    /// <param name="fsType">The filesystem type (optional). <para>文件系统类型（可选）。</para></param>
    /// <param name="size">The partition size in bytes (optional). <para>分区大小（字节，可选）。</para></param>
    /// <param name="options">Additional formatting options (optional). <para>额外的格式化选项（可选）。</para></param>
    /// <returns>A FastbootResponse indicating the result of the operation. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse FormatPartitionWithSlot(string partition, string? slotOverride, string? fsType = null, long? size = null, string? options = null)
    {
        string targetPartition = partition;
        if (HasSlot(partition))
        {
            targetPartition = partition + "_" + (slotOverride ?? GetCurrentSlot());
        }

        return FormatPartitionInternal(targetPartition, fsType, size, options);
    }

    /// <summary>
    /// Formats the specified partition without adding a slot suffix.
    /// <para>格式化指定分区，不添加槽位后缀。</para>
    /// </summary>
    /// <param name="partition">The partition to format. <para>要格式化的分区。</para></param>
    /// <param name="fsType">The filesystem type (optional). <para>文件系统类型（可选）。</para></param>
    /// <param name="size">The partition size in bytes (optional). <para>分区大小（字节，可选）。</para></param>
    /// <param name="options">Additional formatting options (optional). <para>额外的格式化选项（可选）。</para></param>
    /// <returns>A FastbootResponse indicating the result of the operation. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse FormatPartitionNoSlot(string partition, string? fsType = null, long? size = null, string? options = null)
    {
        return FormatPartitionInternal(partition, fsType, size, options);
    }

    private FastbootResponse FormatPartitionInternal(string partition, string? fsType, long? size, string? options)
    {
        NotifyCurrentStep($"Formatting '{partition}'");
        string command = "format";
        if (!string.IsNullOrEmpty(fsType))
        {
            command += ":" + fsType;
            if (size.HasValue)
            {
                command += ":" + size.Value;
            }
        }

        command += ":" + partition;

        if (!string.IsNullOrWhiteSpace(options))
        {
            command += ":" + options;
        }

        return RawCommand(command);
    }
}
