using FirmwareKit.Comm.Fastboot.DataModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    // AOSP constant for response size
    private const int FB_RESPONSE_SZ = 256;
    // AOSP constant for max download size from remote (should match device)
    private const long MAX_DOWNLOAD_SIZE = 1024L * 1024 * 1024 * 4;

    /// <summary>
    /// Handles the request
    /// </summary>
    public FastbootResponse HandleResponse()
    {
        FastbootResponse response = new FastbootResponse();
        DateTime start = DateTime.Now;
        while ((DateTime.Now - start) < TimeSpan.FromSeconds(ReadTimeoutSeconds))
        {
            byte[] data;
            try
            {
                data = Transport.Read(FB_RESPONSE_SZ);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 121)
            {
                response.Result = FastbootState.Timeout;
                response.Response = "status read timeout (121)";
                return response;
            }
            catch (Exception e)
            {
                response.Result = FastbootState.Fail;
                response.Response = "status read failed: " + e.Message;
                return response;
            }

            if (data.Length == 0)
            {
                if ((DateTime.Now - start).TotalSeconds > 2)
                {
                    response.Result = FastbootState.Timeout;
                    response.Response = "status read timed out (no data)";
                    return response;
                }
                continue;
            }

            string devStatus = Encoding.UTF8.GetString(data);
            if (devStatus.Length < 4)
            {
                // Try reading again to see if we can get the rest, or treat as malformed
                continue;
            }

            // Remove any potential leading/trailing junk that can happen if the buffer was recycled
            // though with Array.Empty/New it shouldn't, but Fastboot is ASCII-based.
            devStatus = devStatus.Trim('\0', '\r', '\n');
            if (devStatus.Length < 4) continue;
            string prefix = devStatus.Substring(0, 4);
            string content = devStatus.Length > 4 ? devStatus.Substring(4) : "";
            if (FastbootDebug.IsEnabled && devStatus != prefix + content)
            {
                FastbootDebug.Log($"Raw response bytes: {BitConverter.ToString(data)}");
            }

            if (prefix == "OKAY")
            {
                response.Result = FastbootState.Success;
                response.Response = content;
                return response;
            }
            else if (prefix == "FAIL")
            {
                response.Result = FastbootState.Fail;
                response.Response = content;
                return response;
            }
            else if (prefix == "INFO")
            {
                response.Info.Add(content);
                NotifyReceived(FastbootState.Info, content);
                start = DateTime.Now;
            }
            else if (prefix == "TEXT")
            {
                response.Text += content;
                NotifyReceived(FastbootState.Text, null, content);
                start = DateTime.Now;
            }
            else if (prefix == "DATA")
            {
                string dataHex = content.Trim();
                if (!long.TryParse(dataHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long dsize))
                {
                    response.Result = FastbootState.Fail;
                    response.Response = "data size malformed: " + dataHex;
                    return response;
                }

                if (dsize > MAX_DOWNLOAD_SIZE)
                {
                    response.Result = FastbootState.Fail;
                    response.Response = "data size too large " + dsize;
                    return response;
                }

                response.Result = FastbootState.Data;
                response.DataSize = dsize;
                return response;
            }
            else
            {
                response.Result = FastbootState.Unknown;
                response.Response = "device sent unknown status code: " + devStatus;
                return response;
            }
        }
        response.Result = FastbootState.Timeout;
        return response;
    }


}






