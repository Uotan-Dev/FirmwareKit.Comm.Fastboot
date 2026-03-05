using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    public FastbootResponse ErasePartition(string partition)
    {
        // AOSP erase does not automatically append slot.
        // FastbootDriver::Erase(const std::string& partition, ...)
        // return RawCommand(FB_CMD_ERASE ":" + partition, ...);
        return RawCommand("erase:" + partition);
    }


}






