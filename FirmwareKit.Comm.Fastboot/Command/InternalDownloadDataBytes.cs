using System.Buffers;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Downloads a byte array to the device. Sends the data in chunks based on OnceSendDataSize.
    /// <para>将字节数组下载到设备。根据 OnceSendDataSize 分块发送数据。</para>
    /// </summary>
    /// <param name="data">The data bytes to download to the device. <para>要下载到设备的数据字节。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
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
        {
            return response;
        }
        if (response.DataSize != data.Length)
        {
            FastbootDebug.Log($"Download size mismatch: requested {data.Length} bytes, device accepted {response.DataSize} bytes");
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

    /// <summary>
    /// Downloads a file to the device by reading it from disk and sending its contents.
    /// <para>通过从磁盘读取文件并发送其内容来将文件下载到设备。</para>
    /// </summary>
    /// <param name="filePath">Path to the file to download. <para>要下载的文件的路径。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse DownloadData(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return DownloadData(fs, fs.Length);
    }
}
