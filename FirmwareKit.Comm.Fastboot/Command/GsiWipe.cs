

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    public FastbootResponse GsiWipe() => GsiCommand("wipe");


}






