
using Force.Crc32;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Downloads data with retry and recovery
    /// </summary>
    public FastbootResponse DownloadData(Stream stream, long length, bool onEvent = true)
    {
        const int MaxRetries = 3;
        int retryCount = 0;

        while (retryCount <= MaxRetries)
        {
            try
            {
                // Reset stream to start for each full-transfer attempt
                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);

                return DownloadDataInternal(stream, length, onEvent);
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount > MaxRetries)
                {
                    return new FastbootResponse
                    {
                        Result = FastbootState.Fail,
                        Response = "Max retries exceeded: " + ex.Message
                    };
                }

                NotifyCurrentStep("Download error (" + ex.Message + "), retrying " + retryCount + "/" + MaxRetries + "...");

                // Clear hardware pipes and wait a bit
                try { ResetTransport(); } catch { }
                System.Threading.Thread.Sleep(1000);
            }
        }

        return new FastbootResponse { Result = FastbootState.Fail, Response = "Unknown download failure" };
    }

    private FastbootResponse DownloadDataInternal(Stream stream, long length, bool onEvent)
    {
        if (length <= 0 || length > uint.MaxValue)
        {
            return new FastbootResponse
            {
                Result = FastbootState.Fail,
                Response = "invalid download size"
            };
        }

        bool useCrc = HasCrc();

        // AOSP uses %08" PRIx32 which is 8 chars hex with leading zeros
        FastbootResponse response = RawCommand("download:" + length.ToString("x8"));
        if (response.Result != FastbootState.Data)
            return response;

        byte[] buffer = new byte[OnceSendDataSize];
        long bytesWritten = 0;
        uint crc = 0;

        while (bytesWritten < length)
        {
            int toRead = (int)Math.Min(OnceSendDataSize, length - bytesWritten);
            int readSize = stream.Read(buffer, 0, toRead);
            if (readSize <= 0)
            {
                throw new Exception("stream ended early: " + bytesWritten + "/" + length);
            }

            if (useCrc)
            {
                crc = Crc32Algorithm.Append(crc, buffer, 0, readSize);
            }

            long written = Transport.Write(buffer, readSize);
            if (written != readSize)
            {
                throw new Exception("Short write: " + written + "/" + readSize);
            }
            bytesWritten += written;
            if (onEvent)
                NotifyProgress(bytesWritten, length);
        }

        var finalRes = HandleResponse();

        if (useCrc && finalRes.Result == FastbootState.Success)
        {
            string resp = finalRes.Response.Trim();
            if (resp.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    uint deviceCrc = Convert.ToUInt32(resp, 16);
                    if (deviceCrc != crc)
                    {
                        throw new Exception("CRC Mismatch: Device 0x" + deviceCrc.ToString("x8") + " != Host 0x" + crc.ToString("x8"));
                    }
                }
                catch (FormatException) { }
            }
        }

        return finalRes;
    }
}
