

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    public FastbootResponse FlashingUnlockCritical() => FlashingCommand("unlock_critical");


}






