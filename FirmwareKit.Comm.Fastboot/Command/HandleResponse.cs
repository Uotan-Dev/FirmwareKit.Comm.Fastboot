using System;
using System.Globalization;
using System.Text;
using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
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
                catch (Exception e)
                {
                    response.Result = FastbootState.Fail;
                    response.Response = "status read failed: " + e.Message;
                    return response;
                }

                if (data.Length == 0) continue;

                string devStatus = Encoding.UTF8.GetString(data);
                if (devStatus.Length < 4)
                {
                    response.Result = FastbootState.Fail;
                    response.Response = "status malformed";
                    return response;
                }

                string prefix = devStatus.Substring(0, 4);
                string content = devStatus.Substring(4);

                if (prefix == "OKAY" || prefix == "FAIL")
                {
                    response.Result = prefix == "OKAY" ? FastbootState.Success : FastbootState.Fail;
                    response.Response = content;
                    return response;
                }
                else if (prefix == "INFO")
                {
                    response.Info.Add(content);
                    ReceivedFromDevice?.Invoke(this, new FastbootReceivedFromDeviceEventArgs(FastbootState.Info, content));
                    start = DateTime.Now;
                }
                else if (prefix == "TEXT")
                {
                    response.Text += content;
                    ReceivedFromDevice?.Invoke(this, new FastbootReceivedFromDeviceEventArgs(FastbootState.Text, null, content));
                    start = DateTime.Now;
                }
                else if (prefix == "DATA")
                {
                    if (!long.TryParse(content, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long dsize))
                    {
                        response.Result = FastbootState.Fail;
                        response.Response = "data size malformed: " + content;
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
}
