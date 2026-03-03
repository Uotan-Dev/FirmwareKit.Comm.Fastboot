using FirmwareKit.Comm.Fastboot.DataModel;
using FirmwareKit.Sparse.Core;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Flashes sparse file object
    /// </summary>
    public FastbootResponse FlashSparseFile(string partition, SparseFile sfile, long maxDownloadSize)
    {
        bool useCrc = HasCrc();
        int count = 1;
        FastbootResponse response = new FastbootResponse();
        var parts = sfile.Resparse(maxDownloadSize);
        foreach (var item in parts)
        {
            using Stream stream = item.GetExportStream(0, item.Header.TotalBlocks, useCrc);
            NotifyCurrentStep($"Sending {partition}({count} / {parts.Count})" + (useCrc ? " (with CRC)" : ""));
            DownloadData(stream, stream.Length).ThrowIfError();
            NotifyCurrentStep($"Flashing {partition}({count} / {parts.Count})");
            response = RawCommand("flash:" + partition);
            response.ThrowIfError();
            count++;
        }
        return response;
    }


}