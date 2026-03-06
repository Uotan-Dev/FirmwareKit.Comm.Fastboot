using FirmwareKit.Comm.Fastboot.DataModel;
using FirmwareKit.Sparse.Core;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Flashes sparse image (Already Error check)
    /// </summary>
    public FastbootResponse FlashSparseImage(string partition, string filePath)
    {
        long maxDownloadSize = GetMaxDownloadSize();
        using SparseFile sfile = SparseFile.FromImageFile(filePath);
        return FlashSparseFile(partition, sfile, maxDownloadSize);
    }


}






