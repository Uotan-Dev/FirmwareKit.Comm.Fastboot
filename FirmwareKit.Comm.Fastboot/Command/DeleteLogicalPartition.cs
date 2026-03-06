

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Deletes a logical partition
    /// </summary>
    public FastbootResponse DeleteLogicalPartition(string partition)
    {
        EnsureUserspace();
        return RawCommand($"delete-logical-partition:{partition}");
    }


}






