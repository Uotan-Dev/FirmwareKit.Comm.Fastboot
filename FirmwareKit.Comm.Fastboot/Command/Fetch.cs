namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Fetches a partition image from the device and saves it to a file.
    /// <para>从设备获取分区镜像并保存到文件。</para>
    /// </summary>
    /// <param name="partition">The partition to fetch (e.g., "boot", "system"). <para>要获取的分区（如 "boot"、"system"）。</para></param>
    /// <param name="outputPath">Path to save the fetched image. <para>保存获取镜像的路径。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Fetch(string partition, string outputPath)
    {
        using var fs = File.Create(outputPath);
        return FetchToStream(partition, fs);
    }

    /// <summary>
    /// Fetches a portion of a partition image from the device and saves it to a file.
    /// <para>从设备获取分区镜像的一部分并保存到文件。</para>
    /// </summary>
    /// <param name="partition">The partition to fetch (e.g., "boot", "system"). <para>要获取的分区（如 "boot"、"system"）。</para></param>
    /// <param name="outputPath">Path to save the fetched image. <para>保存获取镜像的路径。</para></param>
    /// <param name="offset">Byte offset within the partition to start fetching. <para>分区内的字节偏移量开始获取。</para></param>
    /// <param name="size">Number of bytes to fetch, or -1 for entire partition. <para>要获取的字节数，-1 表示整个分区。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Fetch(string partition, string outputPath, long offset, long size = -1)
    {
        using var fs = File.Create(outputPath);
        return FetchToStream(partition, fs, offset, size);
    }

    /// <summary>
    /// Fetches a partition image from the device and writes it to a stream.
    /// Automatically handles large partitions by fetching in chunks if needed.
    /// <para>从设备获取分区镜像并写入流。
    /// 如有需要，自动处理大分区，分块获取。</para>
    /// </summary>
    /// <param name="partition">The partition to fetch (e.g., "boot", "system"). <para>要获取的分区（如 "boot"、"system"）。</para></param>
    /// <param name="output">The stream to write the fetched data to. <para>写入获取数据的流。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse FetchToStream(string partition, Stream output)
    {
        string targetPartition = partition;
        if (HasSlot(partition))
        {
            targetPartition = partition + "_" + GetCurrentSlot();
        }

        string szVar = GetVar("partition-size:" + targetPartition);
        long size = -1;
        if (!string.IsNullOrEmpty(szVar) && szVar.StartsWith("0x"))
            size = Convert.ToInt64(szVar, 16);
        else if (!string.IsNullOrEmpty(szVar))
            size = long.Parse(szVar);

        long maxFetchSize = GetMaxFetchSize();
        if (size > 0 && maxFetchSize > 0 && size > maxFetchSize)
        {
            NotifyCurrentStep($"Partition {targetPartition} is larger than max-fetch-size, fetching in chunks...");
            long fetched = 0;
            while (fetched < size)
            {
                long chunk = Math.Min(maxFetchSize, size - fetched);
                // AOSP fastboot_driver.cpp FetchToFd 用 ":0x%08PRIx64"（0x 前缀 + 8 位零填充）；
                // 本地采用裸小写 hex（{x}），fastbootd 用 strtoull 以 16 为基数解析，两种格式
                // 均可接受，且裸 hex 不受 32 位宽度截断影响。
                string cmd = $"fetch:{targetPartition}:{fetched:x}:{chunk:x}";
                var res = UploadData(cmd, output);
                if (res.Result != FastbootState.Success) return res;
                fetched += chunk;
                NotifyProgress(fetched, size);
            }
            return new FastbootResponse { Result = FastbootState.Success };
        }

        return UploadData("fetch:" + targetPartition, output);
    }

    /// <summary>
    /// Fetches a portion of a partition image from the device and writes it to a stream.
    /// Supports specifying offset and size for partial partition reads.
    /// <para>从设备获取分区镜像的一部分并写入流。
    /// 支持指定偏移量和大小以进行部分分区读取。</para>
    /// </summary>
    /// <param name="partition">The partition to fetch (e.g., "boot", "system"). <para>要获取的分区（如 "boot"、"system"）。</para></param>
    /// <param name="output">The stream to write the fetched data to. <para>写入获取数据的流。</para></param>
    /// <param name="offset">Byte offset within the partition to start fetching. <para>分区内的字节偏移量开始获取。</para></param>
    /// <param name="size">Number of bytes to fetch, or -1 for entire partition. <para>要获取的字节数，-1 表示整个分区。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse FetchToStream(string partition, Stream output, long offset, long size = -1)
    {
        string targetPartition = partition;
        if (HasSlot(partition))
        {
            targetPartition = partition + "_" + GetCurrentSlot();
        }

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
            long fetched = 0;
            while (fetched < size)
            {
                long chunk = Math.Min(maxFetchSize, size - fetched);
                string cmd = $"fetch:{targetPartition}:{(offset + fetched):x}:{chunk:x}";
                var res = UploadData(cmd, output);
                if (res.Result != FastbootState.Success) return res;
                fetched += chunk;
                NotifyProgress(fetched, size);
            }
            return new FastbootResponse { Result = FastbootState.Success };
        }

        string finalCmd = $"fetch:{targetPartition}:{offset:x}";
        if (size >= 0)
        {
            finalCmd += $":{size:x}";
        }

        return UploadData(finalCmd, output);
    }

    private long GetMaxFetchSize()
    {
        string val = GetVar("max-fetch-size");
        if (string.IsNullOrEmpty(val)) return -1;
        if (val.StartsWith("0x")) return Convert.ToInt64(val, 16);
        return long.Parse(val);
    }
}
