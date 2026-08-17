
using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    // AOSP constant for response size
    private const int FB_RESPONSE_SZ = 256;
    private static readonly char[] ResponseDelimiters = ['\0', '\r', '\n'];
    // AOSP constant for max download size from remote (should match device)
    // DATA size is an 8-hex-digit (32-bit) field; matches AOSP MAX_DOWNLOAD_SIZE = UINT32_MAX.
    private const long MAX_DOWNLOAD_SIZE = uint.MaxValue;

    /// <summary>
    /// Handles the response from the device, parsing the fastboot protocol response state.
    /// Reads response data from the transport and returns a FastbootResponse with the parsed state.
    /// Supports OKAY, FAIL, INFO, TEXT, and DATA response types.
    /// <para>处理来自设备的响应，解析 fastboot 协议响应状态。
    /// 从传输层读取响应数据并返回带有解析状态的 FastbootResponse。
    /// 支持 OKAY、FAIL、INFO、TEXT 和 DATA 响应类型。</para>
    /// </summary>
    /// <returns>A FastbootResponse containing the parsed response state and data. <para>包含解析响应状态和数据的 FastbootResponse。</para></returns>
    public FastbootResponse HandleResponse()
    {
        FastbootDebug.Log($"HandleResponse()");
        FastbootResponse response = new FastbootResponse();
        // Use the monotonic clock (Stopwatch) for the response timeout so that system
        // clock adjustments cannot corrupt the deadline, matching AOSP's steady_clock.
        long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        long timeoutTicks = (long)(System.Diagnostics.Stopwatch.Frequency * (double)ReadTimeoutSeconds);
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

        // Extracts the content following a 4-char status prefix at `start`, trimming a single
        // trailing NUL. Avoids the double allocation of Substring(...).TrimEnd('\0') and returns
        // the cached empty string for the common empty-OKAY case (zero allocation).
        static string ExtractContent(string s, int start)
        {
            int len = s.Length - start;
            if (len <= 0) return string.Empty;
            if (s[s.Length - 1] == '\0') len--;
            return len == 0 ? string.Empty : s.Substring(start, len);
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

        // Reuse a single pooled buffer across status reads to avoid a 256-byte allocation per
        // packet. All production transports (USB/TCP/UDP) implement IFastbootBufferedTransport;
        // the Read() fallback is kept for custom/mock transports that only implement the base
        // interface.
        bool isBuffered = Transport is IFastbootBufferedTransport;
        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(FB_RESPONSE_SZ);
        try
        {
            while ((System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) < timeoutTicks)
            {
                int readLen;
                try
                {
                    readLen = isBuffered
                        ? ((IFastbootBufferedTransport)Transport).ReadInto(readBuffer, 0, FB_RESPONSE_SZ)
                        : ReadAllIntoFallback(readBuffer, FB_RESPONSE_SZ);
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 121)
                {
                    response.Result = FastbootState.Timeout;
                    response.Response = "status read timeout (121)";
                    FastbootDebug.Log($"Response(Timeout)");
                    return response;
                }
                catch (SocketException ex) when (ex.SocketErrorCode is SocketError.TimedOut
                                                  or SocketError.WouldBlock
                                                  or SocketError.TryAgain)
                {
                    response.Result = FastbootState.Timeout;
                    response.Response = "status read timeout (" + ex.SocketErrorCode + ")";
                    FastbootDebug.Log($"Response(Timeout)");
                    return response;
                }
                catch (System.IO.IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: var code }
                                                       && code is SocketError.TimedOut
                                                          or SocketError.WouldBlock
                                                          or SocketError.TryAgain)
                {
                    response.Result = FastbootState.Timeout;
                    response.Response = "status read timeout (" + code + ")";
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

                if (readLen == 0)
                {
                    // Keep waiting until the global timeout budget expires, matching AOSP behavior.
                    Thread.Sleep(10);
                    continue;
                }

                pendingStatus += Encoding.UTF8.GetString(readBuffer, 0, readLen);
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
                        string content = ExtractContent(pendingStatus, pendingOffset + 4);
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
                        string content = ExtractContent(pendingStatus, pendingOffset + 4);
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
                        startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                        continue;
                    }
                    else if (isData)
                    {
                        // DATA is expected to carry a hex size field and no extra payload.
                        string dataHex = ExtractContent(pendingStatus, pendingOffset + 4).Trim();
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
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }

        // Fallback for transports that only implement IFastbootTransport (e.g. test mocks):
        // read into a temporary array and copy into the caller-provided pooled buffer.
        int ReadAllIntoFallback(byte[] buffer, int length)
        {
            byte[] data = Transport.Read(length);
            int n = data.Length;
            if (n > 0)
            {
                Buffer.BlockCopy(data, 0, buffer, 0, n);
            }
            return n;
        }
    }
}






