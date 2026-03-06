

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    public FastbootResponse FlashingLock() => FlashingCommand("lock");


}






