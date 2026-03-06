

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sends signature file
    /// </summary>
    public FastbootResponse Signature(byte[] sigData)
    {
        DownloadData(sigData).ThrowIfError();
        return RawCommand("signature");
    }


}






