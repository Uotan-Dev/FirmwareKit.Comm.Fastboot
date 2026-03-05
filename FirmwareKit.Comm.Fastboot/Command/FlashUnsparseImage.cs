using FirmwareKit.Comm.Fastboot.DataModel;
using FirmwareKit.Sparse.Core;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Flashes non-sparse image, automatically handling sparse split if larger than max-download-size
    /// </summary>
    public FastbootResponse FlashUnsparseImage(string partition, Stream stream, long length)
    {
        long maxDownloadSize = GetMaxDownloadSize();
        if (length > maxDownloadSize)
        {
            NotifyCurrentStep($"{partition} is too large for a single download, splitting into sparse chunks...");
            // Use SparseFile to split raw image into manageable sparse chunks
            using var sfile = SparseFile.FromStream(stream);
            return FlashSparseFile(partition, sfile, maxDownloadSize);
        }

        NotifyCurrentStep($"Sending {partition} ({length} bytes)");
        DownloadData(stream, length).ThrowIfError();
        NotifyCurrentStep($"Flashing {partition}");
        return RawCommand("flash:" + partition).ThrowIfError();
    }


}