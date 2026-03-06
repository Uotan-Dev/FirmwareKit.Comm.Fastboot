

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Executes GSI-related commands
    /// </summary>
    public FastbootResponse GsiCommand(string subCmd) => RawCommand("gsi:" + subCmd);


}






