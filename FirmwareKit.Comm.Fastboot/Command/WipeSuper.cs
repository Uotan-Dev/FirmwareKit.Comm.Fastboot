using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Clears Super partition metadata
    /// </summary>
    public FastbootResponse WipeSuper(string partition) => RawCommand("wipe-super:" + partition);


}