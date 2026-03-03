using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        public FastbootResponse FlashingUnlockCritical() => FlashingCommand("unlock_critical");
    }
}
