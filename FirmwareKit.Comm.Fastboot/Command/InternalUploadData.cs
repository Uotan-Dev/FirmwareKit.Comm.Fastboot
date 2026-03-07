

using System.Buffers;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Uploads data from device (corresponding to the upload in the protocol)
    /// </summary>
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






