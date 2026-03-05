using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Executes legacy upload command, returning device image or log (like upload:last_kmsg)
    /// </summary>
    public FastbootResponse Upload(string filename, string outputPath)
    {
        using var fs = File.Create(outputPath);
        return UploadData("upload:" + filename, fs);
    }


}






