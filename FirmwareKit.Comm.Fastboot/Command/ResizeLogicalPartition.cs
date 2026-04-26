namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Resizes a logical partition to the specified size. Requires userspace fastboot mode.
    /// <para>将逻辑分区调整为指定大小。需要用户空间 fastboot 模式。</para>
    /// </summary>
    public FastbootResponse ResizeLogicalPartition(string partition, long size)
    {
        EnsureUserspace();
        return RawCommand($"resize-logical-partition:{partition}:{size}");
    }
}
