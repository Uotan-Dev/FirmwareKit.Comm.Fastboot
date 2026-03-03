using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
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
}
