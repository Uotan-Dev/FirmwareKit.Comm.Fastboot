using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        public FastbootResponse ErasePartition(string partition)
        {
            if (HasSlot(partition))
            {
                partition += "_" + GetCurrentSlot();
            }
            return RawCommand("erase:" + partition);
        }
    }
}
