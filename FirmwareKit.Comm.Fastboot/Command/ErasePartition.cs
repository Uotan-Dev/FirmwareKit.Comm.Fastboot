namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Erases the specified partition, automatically using the current active slot if the partition has slots.
    /// <para>擦除指定分区，如果分区有槽位，则自动使用当前活跃槽位。</para>
    /// </summary>
    /// <param name="partition">The partition to erase. <para>要擦除的分区。</para></param>
    /// <returns>A FastbootResponse indicating the result of the operation. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse ErasePartition(string partition)
    {
        string targetPartition = HasSlot(partition)
            ? partition + "_" + GetCurrentSlot()
            : partition;
        NotifyCurrentStep($"Erasing '{targetPartition}'");
        return RawCommand("erase:" + targetPartition);
    }

    /// <summary>
    /// Erases the specified partition without adding a slot suffix.
    /// <para>擦除指定分区，不添加槽位后缀。</para>
    /// </summary>
    /// <param name="partition">The partition to erase. <para>要擦除的分区。</para></param>
    /// <returns>A FastbootResponse indicating the result of the operation. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse ErasePartitionNoSlot(string partition)
    {
        NotifyCurrentStep($"Erasing '{partition}'");
        return RawCommand("erase:" + partition);
    }

    /// <summary>
    /// Erases the specified partition with the given slot suffix.
    /// <para>使用指定的槽位后缀擦除分区。</para>
    /// </summary>
    /// <param name="partition">The partition to erase. <para>要擦除的分区。</para></param>
    /// <param name="slot">The slot suffix (e.g., "a" or "b"). <para>槽位后缀（如 "a" 或 "b"）。</para></param>
    /// <returns>A FastbootResponse indicating the result of the operation. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse ErasePartitionWithSlot(string partition, string slot)
    {
        string targetPartition = partition + "_" + slot;
        NotifyCurrentStep($"Erasing '{targetPartition}'");
        return RawCommand("erase:" + targetPartition);
    }
}
