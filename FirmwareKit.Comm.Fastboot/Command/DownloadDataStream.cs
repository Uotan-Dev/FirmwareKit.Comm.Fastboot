using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Downloads data
    /// </summary>
    public FastbootResponse DownloadData(Stream stream, long length, bool onEvent = true)
    {
        if (length <= 0 || length > uint.MaxValue)
        {
            return new FastbootResponse
            {
                Result = FastbootState.Fail,
                Response = "invalid download size"
            };
        }

        // AOSP uses %08" PRIx32 which is 8 chars hex with leading zeros
        FastbootResponse response = RawCommand("download:" + length.ToString("x8"));
        if (response.Result != FastbootState.Data)
            return response;

        byte[] buffer = new byte[OnceSendDataSize];
        long bytesWritten = 0;
        while (bytesWritten < length)
        {
            int toRead = (int)Math.Min(OnceSendDataSize, length - bytesWritten);
            int readSize = stream.Read(buffer, 0, toRead);
            if (readSize <= 0)
            {
                return new FastbootResponse
                {
                    Result = FastbootState.Fail,
                    Response = $"stream ended early: {bytesWritten}/{length}"
                };
            }

            long written = Transport.Write(buffer, readSize);
            if (written != readSize)
            {
                return new FastbootResponse { Result = FastbootState.Fail, Response = $"Short write: {written}/{readSize}" };
            }
            bytesWritten += written;
            if (onEvent)
                NotifyProgress(bytesWritten, length);
        }

        return HandleResponse();
    }


}






