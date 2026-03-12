
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sends the command
    /// </summary>
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






