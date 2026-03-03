using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Formats a partition
    /// </summary>
    public FastbootResponse FormatPartition(string partition) => RawCommand("format:" + partition);


}