using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Creates a logical partition
        /// </summary>
        public FastbootResponse CreateLogicalPartition(string partition, long size)
        {
            EnsureUserspace();
            return RawCommand($"create-logical-partition:{partition}:{size}");
        }
    }
}
