using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    public FastbootResponse FlashingLockCritical() => FlashingCommand("lock_critical");


}