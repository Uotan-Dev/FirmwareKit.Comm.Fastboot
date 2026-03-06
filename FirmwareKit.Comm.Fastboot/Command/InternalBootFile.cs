

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sends and guides the kernel image file
    /// </summary>
    public FastbootResponse Boot(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        DownloadData(fs, fs.Length).ThrowIfError();
        return RawCommand("boot");
    }


}






