namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    private FastbootResponse FlashRawImage(string partition, Stream stream, long imageSize)
    {
        if (imageSize <= 0 || imageSize > uint.MaxValue)
        {
            return new FastbootResponse
            {
                Result = FastbootState.Fail,
                Response = "invalid download size"
            };
        }

        NotifyCurrentStep($"Sending raw image to {partition}...");
        var download = DownloadData(stream, imageSize);
        if (download.Result != FastbootState.Success)
        {
            return download;
        }

        NotifyCurrentStep($"Flashing {partition}...");
        return RawCommand("flash:" + partition);
    }

}






