

using System.Buffers;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Uploads data from the device using the specified command and writes it to a stream.
    /// Used for operations like fetching partition data or retrieving staged data from the device.
    /// <para>使用指定命令从设备上传数据并将其写入流。
    /// 用于从设备获取分区数据或检索暂存数据等操作。</para>
    /// </summary>
    /// <param name="command">The fastboot command to execute (e.g., "upload:partition", "get_staged"). <para>要执行的 fastboot 命令（如 "upload:partition"、"get_staged"）。</para></param>
    /// <param name="output">The stream to write the uploaded data to. <para>写入上传数据的流。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse UploadData(string command, Stream output)
    {
        FastbootResponse response = RawCommand(command);
        if (response.Result != FastbootState.Data)
            throw new Exception("Unexpected response for upload: " + response.Result);

        long size = response.DataSize;
        long bytesDownloaded = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(OnceSendDataSize);
        try
        {
            while (bytesDownloaded < size)
            {
                int toRead = (int)Math.Min(OnceSendDataSize, size - bytesDownloaded);
                int readLen;

                if (Transport is IFastbootBufferedTransport buffered)
                {
                    readLen = buffered.ReadInto(buffer, 0, toRead);
                }
                else
                {
                    byte[] data = Transport.Read(toRead);
                    if (data != null && data.Length > 0)
                    {
                        readLen = data.Length;
                        Buffer.BlockCopy(data, 0, buffer, 0, readLen);
                    }
                    else
                    {
                        readLen = 0;
                    }
                }

                if (readLen <= 0) throw new Exception("Unexpected EOF from USB.");

                output.Write(buffer, 0, readLen);
                bytesDownloaded += readLen;
                NotifyProgress(bytesDownloaded, size);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return HandleResponse();
    }


}






