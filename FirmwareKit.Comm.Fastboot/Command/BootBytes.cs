using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Sends and guides the kernel (not written to Flash)
        /// </summary>
        public FastbootResponse Boot(byte[] data)
        {
            DownloadData(data).ThrowIfError();
            return RawCommand("boot");
        }
    }
}
