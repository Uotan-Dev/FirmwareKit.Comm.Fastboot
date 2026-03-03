using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Continues the boot process
        /// </summary>
        public FastbootResponse Continue() => RawCommand("continue");
    }
}
