
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sends a raw fastboot command to the device and returns the response.
    /// This is the core method for executing all fastboot protocol commands.
    /// <para>向设备发送原始 fastboot 命令并返回响应。
    /// 这是执行所有 fastboot 协议命令的核心方法。</para>
    /// </summary>
    /// <param name="command">The fastboot command string to send (e.g., "reboot", "getvar:version"). <para>要发送的 fastboot 命令字符串（如 "reboot"、"getvar:version"）。</para></param>
    /// <param name="quiet">If true, suppresses the CommandCompleted event notification. <para>如果为 true，则抑制 CommandCompleted 事件通知。</para></param>
    /// <returns>A FastbootResponse containing the command result and any data. <para>包含命令结果和任何数据的 FastbootResponse。</para></returns>
    public FastbootResponse RawCommand(string command, bool quiet = false)
    {
        FastbootDebug.Log("Sending command: " + command);
        byte[] cmdBytes = Encoding.UTF8.GetBytes(command);
        try
        {
            int bytesWritten = (int)Transport.Write(cmdBytes, cmdBytes.Length);
            FastbootDebug.Log($"Bytes written: {bytesWritten}/{cmdBytes.Length}");
            if (bytesWritten != cmdBytes.Length)
            {
                return new FastbootResponse
                {
                    Result = FastbootState.Fail,
                    Response = "command write failed (short transfer)"
                };
            }
        }
        catch (Exception e)
        {
            FastbootDebug.Log("Exception during command write: " + e);
            return new FastbootResponse
            {
                Result = FastbootState.Fail,
                Response = "command write failed: " + e.Message
            };
        }

        FastbootDebug.Log("Waiting for response...");
        var response = HandleResponse();
        FastbootDebug.Log("Response received: " + response.Response);
        NotifyCommandCompleted(command, response, quiet);

        return response;
    }
}






