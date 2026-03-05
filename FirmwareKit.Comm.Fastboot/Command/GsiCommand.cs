using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Executes GSI-related commands
    /// </summary>
    public FastbootResponse GsiCommand(string subCmd) => RawCommand("gsi:" + subCmd);


}






