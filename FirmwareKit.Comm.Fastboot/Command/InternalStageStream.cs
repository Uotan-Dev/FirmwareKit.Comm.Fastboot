

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Executes stage command, sending local data to device memory
    /// </summary>
    public FastbootResponse Stage(Stream stream, long length)
    {
        NotifyCurrentStep("Staging data from stream...");
        FastbootResponse downloadRes = DownloadData(stream, length);
        if (downloadRes.Result != FastbootState.Success) return downloadRes;

        return RawCommand("stage");
    }


}






