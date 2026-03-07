

using System.Buffers;

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
        if (response.DataSize != data.Length)
        {
            return new FastbootResponse
            {
                Result = FastbootState.Fail,
                Response = $"download size mismatch: requested {data.Length}, device accepted {response.DataSize}"
            };
        }

        long bytesWritten = 0;
        int length = data.Length;
        byte[] transferBuffer = ArrayPool<byte>.Shared.Rent(Math.Min(OnceSendDataSize, length));
        try
        {
            while (bytesWritten < length)
            {
                int toWrite = (int)Math.Min(OnceSendDataSize, length - bytesWritten);
                Buffer.BlockCopy(data, (int)bytesWritten, transferBuffer, 0, toWrite);

                long written = Transport.Write(transferBuffer, toWrite);
                if (written != toWrite)
                {
                    return new FastbootResponse { Result = FastbootState.Fail, Response = $"Short write: {written}/{toWrite}" };
                }
                bytesWritten += written;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(transferBuffer);
        }

        return HandleResponse();
    }


}






