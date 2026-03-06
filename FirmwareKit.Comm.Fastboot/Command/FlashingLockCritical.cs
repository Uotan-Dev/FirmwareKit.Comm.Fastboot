

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    public FastbootResponse FlashingLockCritical() => FlashingCommand("lock_critical");


}






