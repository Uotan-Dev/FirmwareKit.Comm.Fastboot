

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Executes OEM command
    /// </summary>
    public FastbootResponse OemCommand(string oemCmd) => RawCommand("oem " + oemCmd);


}






