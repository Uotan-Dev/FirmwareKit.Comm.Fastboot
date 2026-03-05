using FirmwareKit.Comm.Fastboot.DataModel;
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Sends the command
    /// </summary>
    public FastbootResponse RawCommand(string command)
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

        if (response.Result == FastbootState.Fail)
        {
            if (command.StartsWith("snapshot-update", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Snapshot                                           FAILED (remote: '{response.Response}')");
            }
            else
            {
                Console.Error.WriteLine($"FAILED (remote: '{response.Response}')");
            }
        }
        else if (command.StartsWith("getvar:", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(command, "getvar:all", StringComparison.OrdinalIgnoreCase))
        {
            string key = command.Substring("getvar:".Length);
            Console.Error.WriteLine($"{key}: {response.Response}");
        }
        else if (command.StartsWith("devices"))
        {
            Console.WriteLine(response.Response);
        }
        else if (!string.IsNullOrEmpty(response.Response))
        {
            Console.Error.WriteLine(response.Response);
        }

        return response;
    }


}






