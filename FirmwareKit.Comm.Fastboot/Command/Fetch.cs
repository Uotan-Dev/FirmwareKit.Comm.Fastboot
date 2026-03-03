using System.IO;
using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Fetches data from partition (fetch)
        /// </summary>
        public FastbootResponse Fetch(string partition, string outputPath, long offset = 0, long size = -1)
        {
            string targetPartition = partition;
            if (HasSlot(partition))
            {
                targetPartition = partition + "_" + GetCurrentSlot();
            }

            string cmd = "fetch:" + targetPartition;
            if (offset >= 0)
            {
                cmd += $":0x{offset:x8}";
                if (size >= 0)
                {
                    cmd += $":0x{size:x8}";
                }
            }

            using var fs = File.Create(outputPath);
            return UploadData(cmd, fs);
        }
    }
}
