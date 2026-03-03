using System;
using System.Text;
using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Sends the command
        /// </summary>
        public FastbootResponse RawCommand(string command)
        {
            byte[] cmdBytes = Encoding.UTF8.GetBytes(command);
            try
            {
                if (Transport.Write(cmdBytes, cmdBytes.Length) != cmdBytes.Length)
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
                return new FastbootResponse
                {
                    Result = FastbootState.Fail,
                    Response = "command write failed: " + e.Message
                };
            }

            var response = HandleResponse();

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
}
