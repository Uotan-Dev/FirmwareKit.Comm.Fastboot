

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    public FastbootResponse FlashingUnlock() => FlashingCommand("unlock");


}






