namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sets the active A/B slot on the device.
    /// <para>设置设备上的活跃 A/B 槽位。</para>
    /// </summary>
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
