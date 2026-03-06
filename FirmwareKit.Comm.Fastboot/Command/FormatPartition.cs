

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Formats a partition
    /// </summary>
    public FastbootResponse FormatPartition(string partition)
    {
        NotifyCurrentStep($"Formatting '{partition}'");
        return RawCommand("format:" + partition);
    }


}






