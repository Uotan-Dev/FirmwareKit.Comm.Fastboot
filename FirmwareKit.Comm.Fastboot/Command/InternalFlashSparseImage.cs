
using FirmwareKit.Sparse.Core;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Flashes sparse image with fallback to RAW
    /// </summary>
    public FastbootResponse FlashSparseImage(string partition, string filePath)
    {
        long maxDownloadSize = GetMaxDownloadSize();
        try
        {
            using SparseFile sfile = SparseFile.FromImageFile(filePath);
            return FlashSparseFile(partition, sfile, maxDownloadSize);
        }
        catch (Exception ex)
        {
            NotifyCurrentStep($"Warning: Failed to parse sparse image {filePath} ({ex.Message}). Falling back to RAW flashing...");
            using var fs = File.OpenRead(filePath);
            return FlashUnsparseImage(partition, fs, fs.Length);
        }
    }


}






