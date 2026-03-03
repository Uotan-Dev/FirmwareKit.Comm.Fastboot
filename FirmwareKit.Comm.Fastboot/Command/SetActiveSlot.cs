using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    public FastbootResponse SetActiveSlot(string slot)
    {
        var res = RawCommand("set_active:" + slot);
        if (res.Result == FastbootState.Success)
        {
            _varCache.Remove("current-slot");
        }
        return res;
    }


}