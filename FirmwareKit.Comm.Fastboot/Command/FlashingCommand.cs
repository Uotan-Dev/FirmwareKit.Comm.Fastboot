

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Executes Flashing sub-command (modern unlocking commands)
    /// </summary>
    public FastbootResponse FlashingCommand(string subCmd) => RawCommand("flashing " + subCmd);


}






