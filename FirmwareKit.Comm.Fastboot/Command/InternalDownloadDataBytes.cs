

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Downloads data
    /// </summary>
    public FastbootResponse DownloadData(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return new FastbootResponse
            {
                Result = FastbootState.Fail,
                Response = "invalid download size"
            };
        }

        FastbootResponse response = RawCommand("download:" + data.Length.ToString("x8"));
        if (response.Result != FastbootState.Data)
            return response;

        long bytesWritten = 0;
        int length = data.Length;

        while (bytesWritten < length)
        {
            int toWrite = (int)Math.Min(OnceSendDataSize, length - bytesWritten);
            byte[] chunk = new byte[toWrite];
            Array.Copy(data, bytesWritten, chunk, 0, toWrite);

            long written = Transport.Write(chunk, toWrite);
            if (written != toWrite)
            {
                return new FastbootResponse { Result = FastbootState.Fail, Response = $"Short write: {written}/{toWrite}" };
            }
            bytesWritten += written;
        }

        return HandleResponse();
    }


}






