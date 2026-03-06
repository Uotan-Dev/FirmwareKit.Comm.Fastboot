

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Executes stage command, sending local data to device memory (used for subsequent boot or flash instructions, depending on the device)
    /// </summary>
    public FastbootResponse Stage(byte[] data)
    {
        NotifyCurrentStep("Staging data...");
        FastbootResponse downloadRes = DownloadData(data);
        if (downloadRes.Result != FastbootState.Success) return downloadRes;

        return RawCommand("stage");
    }


}






