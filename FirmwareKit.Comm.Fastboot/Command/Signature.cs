using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
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






