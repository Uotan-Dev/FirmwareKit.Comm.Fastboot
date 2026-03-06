

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    public FastbootResponse ErasePartition(string partition)
    {
        NotifyCurrentStep($"Erasing '{partition}'");
        return RawCommand("erase:" + partition);
    }


}






