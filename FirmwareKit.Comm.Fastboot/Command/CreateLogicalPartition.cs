namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Creates a logical partition with the specified size. Requires userspace fastboot mode.
    /// <para>创建指定大小的逻辑分区。需要用户空间 fastboot 模式。</para>
    /// </summary>
    public FastbootResponse CreateLogicalPartition(string partition, long size)
    {
        FastbootDebug.Log($"CreateLogicalPartition(partition={partition}, size={size})");
        EnsureUserspace();
        return RawCommand($"create-logical-partition:{partition}:{size}");
    }
}
