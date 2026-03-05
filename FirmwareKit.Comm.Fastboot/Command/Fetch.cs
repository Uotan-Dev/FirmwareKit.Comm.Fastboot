using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Fetches data from partition (fetch), automatically handling large fetches in chunks
    /// </summary>
    public FastbootResponse Fetch(string partition, string outputPath, long offset = 0, long size = -1)
    {
        string targetPartition = partition;
        if (HasSlot(partition))
        {
            targetPartition = partition + "_" + GetCurrentSlot();
        }

        // If size is not specified, try to get it from variables
        if (size == -1)
        {
            string szVar = GetVar("partition-size:" + targetPartition);
            if (!string.IsNullOrEmpty(szVar) && szVar.StartsWith("0x"))
                size = Convert.ToInt64(szVar, 16);
            else if (!string.IsNullOrEmpty(szVar))
                size = long.Parse(szVar);
        }

        long maxFetchSize = GetMaxFetchSize();
        if (size > 0 && maxFetchSize > 0 && size > maxFetchSize)
        {
            NotifyCurrentStep($"Partition {targetPartition} is larger than max-fetch-size, fetching in chunks...");
            using var fs = File.Create(outputPath);
            long fetched = 0;
            while (fetched < size)
            {
                long chunk = Math.Min(maxFetchSize, size - fetched);
                string cmd = $"fetch:{targetPartition}:0x{(offset + fetched):x8}:0x{chunk:x8}";
                var res = UploadData(cmd, fs);
                if (res.Result != FastbootState.Success) return res;
                fetched += chunk;
                NotifyProgress(fetched, size);
            }
            return new FastbootResponse { Result = FastbootState.Success };
        }

        string finalCmd = "fetch:" + targetPartition;
        if (offset >= 0)
        {
            finalCmd += $":0x{offset:x8}";
            if (size >= 0)
            {
                finalCmd += $":0x{size:x8}";
            }
        }

        using var ofs = File.Create(outputPath);
        return UploadData(finalCmd, ofs);
    }

    private long GetMaxFetchSize()
    {
        string val = GetVar("max-fetch-size");
        if (string.IsNullOrEmpty(val)) return -1;
        if (val.StartsWith("0x")) return Convert.ToInt64(val, 16);
        return long.Parse(val);
    }


}






