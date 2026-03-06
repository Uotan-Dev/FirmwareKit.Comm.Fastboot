

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Continues the boot process
    /// </summary>
    public FastbootResponse Continue() => RawCommand("continue");


}






