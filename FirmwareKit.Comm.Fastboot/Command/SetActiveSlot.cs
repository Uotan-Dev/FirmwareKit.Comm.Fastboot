

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    public FastbootResponse SetActiveSlot(string slot)
    {
        NotifyCurrentStep($"Setting current slot to '{slot}'");
        var res = RawCommand("set_active:" + slot);
        if (res.Result == FastbootState.Success)
        {
            _varCache.Remove("current-slot");
        }
        return res;
    }


}






