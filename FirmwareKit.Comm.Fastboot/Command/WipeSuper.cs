

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Clears Super partition metadata
    /// </summary>
    public FastbootResponse WipeSuper(string partition) => RawCommand("wipe-super:" + partition);


}






