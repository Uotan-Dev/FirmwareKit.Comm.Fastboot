using FirmwareKit.Comm.Fastboot.DataModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading;

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
        string pendingStatus = string.Empty;

        static bool IsKnownPrefixAt(string s, int index)
        {
            if (index + 4 > s.Length) return false;
            string p = s.Substring(index, 4);
            return p == "OKAY" || p == "FAIL" || p == "INFO" || p == "TEXT" || p == "DATA";
        }

        static int FindInfoTextEnd(string s, int contentStart)
        {
            int delimiterIdx = s.IndexOfAny(new[] { '\0', '\r', '\n' }, contentStart);
            if (delimiterIdx >= 0)
            {
                return delimiterIdx;
            }

            for (int i = contentStart + 1; i <= s.Length - 4; i++)
            {
                // For INFO/TEXT payloads without delimiters, only treat terminal status
                // prefixes as boundaries. This avoids splitting on normal words such as
                // "TEXT" that may appear in the payload.
                if (i + 4 <= s.Length)
                {
                    string p = s.Substring(i, 4);
                    if (p == "OKAY" || p == "FAIL" || p == "DATA") return i;
                }
            }

            return -1;
        }

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
                // Keep waiting until the global timeout budget expires, matching AOSP behavior.
                Thread.Sleep(10);
                continue;
            }

            pendingStatus += Encoding.UTF8.GetString(data);
            while (true)
            {
                if (pendingStatus.Length < 4)
                {
                    break;
                }

                if (!IsKnownPrefixAt(pendingStatus, 0))
                {
                    int nextPrefix = -1;
                    for (int i = 1; i <= pendingStatus.Length - 4; i++)
                    {
                        if (IsKnownPrefixAt(pendingStatus, i))
                        {
                            nextPrefix = i;
                            break;
                        }
                    }

                    if (nextPrefix > 0)
                    {
                        pendingStatus = pendingStatus.Substring(nextPrefix);
                        continue;
                    }

                    response.Result = FastbootState.Unknown;
                    response.Response = "device sent unknown status code: " + pendingStatus;
                    return response;
                }

                string prefix = pendingStatus.Substring(0, 4);

                if (prefix == "OKAY")
                {
                    string content = pendingStatus.Length > 4 ? pendingStatus.Substring(4).TrimEnd('\0') : "";
                    response.Result = FastbootState.Success;
                    response.Response = content;
                    return response;
                }
                else if (prefix == "FAIL")
                {
                    string content = pendingStatus.Length > 4 ? pendingStatus.Substring(4).TrimEnd('\0') : "";
                    response.Result = FastbootState.Fail;
                    response.Response = content;
                    return response;
                }
                else if (prefix == "INFO" || prefix == "TEXT")
                {
                    int endIdx = FindInfoTextEnd(pendingStatus, 4);
                    if (endIdx < 0)
                    {
                        // Most transports deliver one status frame per read packet.
                        // If no boundary marker exists, treat the current chunk as one
                        // complete INFO/TEXT frame to avoid accidentally merging with
                        // the next status frame.
                        endIdx = pendingStatus.Length;
                    }

                    string cleanContent = pendingStatus.Substring(4, endIdx - 4);
                    if (prefix == "INFO")
                    {
                        response.Info.Add(cleanContent);
                        NotifyReceived(FastbootState.Info, cleanContent);
                    }
                    else
                    {
                        response.Text += cleanContent;
                        NotifyReceived(FastbootState.Text, null, cleanContent);
                    }

                    int next = endIdx;
                    while (next < pendingStatus.Length &&
                           (pendingStatus[next] == '\0' || pendingStatus[next] == '\r' || pendingStatus[next] == '\n'))
                    {
                        next++;
                    }
                    pendingStatus = pendingStatus.Substring(next);
                    start = DateTime.Now;
                    continue;
                }
                else if (prefix == "DATA")
                {
                    // DATA is expected to carry a hex size field and no extra payload.
                    string content = pendingStatus.Length > 4 ? pendingStatus.Substring(4).TrimEnd('\0') : "";
                    string dataHex = content.Trim();
                    if (dataHex.Length == 0 || dataHex.Length > 8)
                    {
                        response.Result = FastbootState.Fail;
                        response.Response = "data size malformed: " + dataHex;
                        return response;
                    }

                    for (int i = 0; i < dataHex.Length; i++)
                    {
                        if (!Uri.IsHexDigit(dataHex[i]))
                        {
                            response.Result = FastbootState.Fail;
                            response.Response = "data size malformed: " + dataHex;
                            return response;
                        }
                    }

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
            }
        }
        response.Result = FastbootState.Timeout;
        return response;
    }


}






