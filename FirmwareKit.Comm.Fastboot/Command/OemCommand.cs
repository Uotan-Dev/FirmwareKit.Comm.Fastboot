using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Executes OEM command
        /// </summary>
        public FastbootResponse OemCommand(string oemCmd) => RawCommand("oem " + oemCmd);
    }
}
