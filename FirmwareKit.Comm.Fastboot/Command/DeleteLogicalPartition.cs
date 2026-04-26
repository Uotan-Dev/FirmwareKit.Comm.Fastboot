namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Deletes a logical partition. Requires userspace fastboot mode.
    /// <para>删除逻辑分区。需要用户空间 fastboot 模式。</para>
    /// </summary>
    public FastbootResponse DeleteLogicalPartition(string partition)
    {
        EnsureUserspace();
        return RawCommand($"delete-logical-partition:{partition}");
    }
}
