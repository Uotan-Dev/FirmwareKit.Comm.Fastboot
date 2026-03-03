using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Executes Flashing sub-command (modern unlocking commands)
    /// </summary>
    public FastbootResponse FlashingCommand(string subCmd) => RawCommand("flashing " + subCmd);


}