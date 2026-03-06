

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    public FastbootResponse GsiStatus() => GsiCommand("status");


}






