

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Adjusts the size of a logical partition
    /// </summary>
    public FastbootResponse ResizeLogicalPartition(string partition, long size)
    {
        EnsureUserspace();
        return RawCommand($"resize-logical-partition:{partition}:{size}");
    }


}






