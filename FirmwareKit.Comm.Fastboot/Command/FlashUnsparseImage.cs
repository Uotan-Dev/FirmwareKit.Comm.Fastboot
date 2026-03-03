using System.IO;
using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Flashes non-sparse image (Already Error check)
        /// </summary>
        public FastbootResponse FlashUnsparseImage(string partition, Stream stream, long length)
        {
            NotifyCurrentStep($"Sending {partition}");
            DownloadData(stream, length).ThrowIfError();
            NotifyCurrentStep($"Flashing {partition}");
            return RawCommand("flash:" + partition).ThrowIfError();
        }
    }
}
