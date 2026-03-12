
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    // AOSP constant for response size
    private const int FB_RESPONSE_SZ = 256;
    private static readonly char[] ResponseDelimiters = ['\0', '\r', '\n'];
    // AOSP constant for max download size from remote (should match device)
    private const long MAX_DOWNLOAD_SIZE = 1024L * 1024 * 1024 * 4;

    /// <summary>
    /// Handles the request
    /// </summary>
    public FastbootResponse HandleResponse()
    {
        FastbootDebug.Log($"HandleResponse()");
        FastbootResponse response = new FastbootResponse();
        DateTime start = DateTime.Now;
        string pendingStatus = string.Empty;
        int pendingOffset = 0;
        StringBuilder? textBuffer = null;

        static void CompactPendingIfNeeded(ref string pending, ref int offset)
        {
            if (offset <= 0) return;

            if (offset > 1024 || offset > pending.Length / 2)
            {
                pending = pending.Substring(offset);
                offset = 0;
            }
        }

        static bool IsPrefixAt(string s, int index, char a, char b, char c, char d)
        {
            return index + 4 <= s.Length &&
                   s[index] == a &&
                   s[index + 1] == b &&
                   s[index + 2] == c &&
                   s[index + 3] == d;
        }

        static bool IsKnownPrefixAt(string s, int index)
        {
            return IsPrefixAt(s, index, 'O', 'K', 'A', 'Y') ||
                   IsPrefixAt(s, index, 'F', 'A', 'I', 'L') ||
                   IsPrefixAt(s, index, 'I', 'N', 'F', 'O') ||
                   IsPrefixAt(s, index, 'T', 'E', 'X', 'T') ||
                   IsPrefixAt(s, index, 'D', 'A', 'T', 'A');
        }

        static int FindInfoTextEnd(string s, int contentStart)
        {
            int delimiterIdx = s.IndexOfAny(ResponseDelimiters, contentStart);
            if (delimiterIdx >= 0)
            {
                return delimiterIdx;
            }

            static bool IsTerminalPrefixAt(string value, int index)
            {
                return IsPrefixAt(value, index, 'O', 'K', 'A', 'Y') ||
                       IsPrefixAt(value, index, 'F', 'A', 'I', 'L') ||
                       IsPrefixAt(value, index, 'D', 'A', 'T', 'A');
            }

            int firstTerminalIdx = -1;
            for (int i = contentStart + 1; i <= s.Length - 4; i++)
            {
                if (IsTerminalPrefixAt(s, i))
                {
                    firstTerminalIdx = i;
                    break;
                }
            }

            for (int i = contentStart + 1; i <= s.Length - 4; i++)
            {
                // For INFO/TEXT payloads without delimiters, treat any known
                // status prefix (OKAY/FAIL/DATA/INFO/TEXT) as a boundary. This
                // allows consecutive INFO/TEXT frames in a single packet to be
                // split properly while still protecting against malformed streams.
                if (i + 4 <= s.Length)
                {
                    if (IsTerminalPrefixAt(s, i)) return i;

                    // Avoid splitting plain payload text that happens to contain
                    // INFO/TEXT tokens. Only split INFO/TEXT mid-payload when the
                    // packet also contains a terminal status marker later.
                    bool isInfoAt = IsPrefixAt(s, i, 'I', 'N', 'F', 'O');
                    bool isTextAt = IsPrefixAt(s, i, 'T', 'E', 'X', 'T');
                    if ((isInfoAt || isTextAt) && firstTerminalIdx > i) return i;
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
                FastbootDebug.Log($"Response(Timeout)");
                return response;
            }
            catch (Exception e)
            {
                response.Result = FastbootState.Fail;
                response.Response = "status read failed: " + e.Message;
                FastbootDebug.Log($"Response(Fail)");
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
                CompactPendingIfNeeded(ref pendingStatus, ref pendingOffset);

                if (pendingStatus.Length - pendingOffset < 4)
                {
                    break;
                }

                if (!IsKnownPrefixAt(pendingStatus, pendingOffset))
                {
                    int nextPrefix = -1;
                    for (int i = pendingOffset + 1; i <= pendingStatus.Length - 4; i++)
                    {
                        if (IsKnownPrefixAt(pendingStatus, i))
                        {
                            nextPrefix = i;
                            break;
                        }
                    }

                    if (nextPrefix > pendingOffset)
                    {
                        pendingOffset = nextPrefix;
                        continue;
                    }

                    response.Result = FastbootState.Unknown;
                    response.Response = "device sent unknown status code: " + pendingStatus.Substring(pendingOffset);
                    FastbootDebug.Log($"Response(Unknown)");
                    return response;
                }

                bool isOkay = IsPrefixAt(pendingStatus, pendingOffset, 'O', 'K', 'A', 'Y');
                bool isFail = IsPrefixAt(pendingStatus, pendingOffset, 'F', 'A', 'I', 'L');
                bool isInfo = IsPrefixAt(pendingStatus, pendingOffset, 'I', 'N', 'F', 'O');
                bool isText = IsPrefixAt(pendingStatus, pendingOffset, 'T', 'E', 'X', 'T');
                bool isData = IsPrefixAt(pendingStatus, pendingOffset, 'D', 'A', 'T', 'A');

                if (isOkay)
                {
                    string content = pendingStatus.Length > pendingOffset + 4 ? pendingStatus.Substring(pendingOffset + 4).TrimEnd('\0') : "";
                    response.Result = FastbootState.Success;
                    if (textBuffer != null)
                    {
                        response.Text = textBuffer.ToString();
                    }
                    response.Response = content;
                    FastbootDebug.Log($"Response(Success)");
                    return response;
                }
                else if (isFail)
                {
                    string content = pendingStatus.Length > pendingOffset + 4 ? pendingStatus.Substring(pendingOffset + 4).TrimEnd('\0') : "";
                    response.Result = FastbootState.Fail;
                    if (textBuffer != null)
                    {
                        response.Text = textBuffer.ToString();
                    }
                    response.Response = content;
                    FastbootDebug.Log($"Response(Fail)");
                    return response;
                }
                else if (isInfo || isText)
                {
                    int contentStart = pendingOffset + 4;
                    int endIdx = FindInfoTextEnd(pendingStatus, contentStart);
                    if (endIdx < 0)
                    {
                        // Most transports deliver one status frame per read packet.
                        // If no boundary marker exists, treat the current chunk as one
                        // complete INFO/TEXT frame to avoid accidentally merging with
                        // the next status frame.
                        endIdx = pendingStatus.Length;
                    }

                    string cleanContent = pendingStatus.Substring(contentStart, endIdx - contentStart);
                    if (isInfo)
                    {
                        response.Info.Add(cleanContent);
                        NotifyReceived(FastbootState.Info, cleanContent);
                    }
                    else
                    {
                        textBuffer ??= new StringBuilder();
                        textBuffer.Append(cleanContent);
                        NotifyReceived(FastbootState.Text, null, cleanContent);
                    }

                    int next = endIdx;
                    while (next < pendingStatus.Length &&
                           (pendingStatus[next] == '\0' || pendingStatus[next] == '\r' || pendingStatus[next] == '\n'))
                    {
                        next++;
                    }
                    pendingOffset = next;
                    start = DateTime.Now;
                    continue;
                }
                else if (isData)
                {
                    // DATA is expected to carry a hex size field and no extra payload.
                    string content = pendingStatus.Length > pendingOffset + 4 ? pendingStatus.Substring(pendingOffset + 4).TrimEnd('\0') : "";
                    string dataHex = content.Trim();
                    if (dataHex.Length == 0 || dataHex.Length > 8)
                    {
                        response.Result = FastbootState.Fail;
                        response.Response = "data size malformed: " + dataHex;
                        FastbootDebug.Log($"Response(Fail)");
                        return response;
                    }

                    for (int i = 0; i < dataHex.Length; i++)
                    {
                        if (!Uri.IsHexDigit(dataHex[i]))
                        {
                            response.Result = FastbootState.Fail;
                            response.Response = "data size malformed: " + dataHex;
                            FastbootDebug.Log($"Response(Fail)");
                            return response;
                        }
                    }

                    if (!long.TryParse(dataHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long dsize))
                    {
                        response.Result = FastbootState.Fail;
                        response.Response = "data size malformed: " + dataHex;
                        FastbootDebug.Log($"Response(Fail)");
                        return response;
                    }

                    if (dsize > MAX_DOWNLOAD_SIZE)
                    {
                        response.Result = FastbootState.Fail;
                        response.Response = "data size too large " + dsize;
                        FastbootDebug.Log($"Response(Fail)");
                        return response;
                    }

                    response.Result = FastbootState.Data;
                    response.DataSize = dsize;
                    if (textBuffer != null)
                    {
                        response.Text = textBuffer.ToString();
                    }
                    FastbootDebug.Log($"Response(Data)");
                    return response;
                }
            }
        }
        response.Result = FastbootState.Timeout;
        FastbootDebug.Log($"Response(Timeout)");
        return response;
    }


}






