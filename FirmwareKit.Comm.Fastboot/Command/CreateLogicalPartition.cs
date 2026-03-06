

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Creates a logical partition
    /// </summary>
    public FastbootResponse CreateLogicalPartition(string partition, long size)
    {
        FastbootDebug.Log($"CreateLogicalPartition(partition={partition}, size={size})");
        EnsureUserspace();
        return RawCommand($"create-logical-partition:{partition}:{size}");
    }


}






