namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Creates and flashes a raw boot image to the specified partition.
    /// Supports multiple boot image header versions (0-6) and optional components like ramdisk, second stage, and DTB.
    /// <para>创建并刷写原始启动镜像到指定分区。
    /// 支持多种启动镜像头版本（0-6）以及可选组件如 ramdisk、第二阶段和 DTB。</para>
    /// </summary>
    /// <param name="partition">Target partition to flash (e.g., "boot", "recovery"). <para>目标刷写分区（如 "boot"、"recovery"）。</param>
    /// <param name="kernelPath">Path to the kernel image file. <para>内核镜像文件路径。</para></param>
    /// <param name="ramdiskPath">Path to the ramdisk image file (optional). <para>ramdisk 镜像文件路径（可选）。</param>
    /// <param name="secondPath">Path to the second stage loader image file (optional). <para>第二阶段加载器镜像文件路径（可选）。</param>
    /// <param name="dtbPath">Path to the DTB image file (optional). <para>DTB 镜像文件路径（可选）。</param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</param>
    /// <param name="header_version">Boot image header version (0-6). Default is 0. <para>启动镜像头版本（0-6）。默认为 0。</param>
    /// <param name="base_addr">Base memory address. Default is 0x10000000. <para>基本内存地址。默认为 0x10000000。</param>
    /// <param name="page_size">Flash page size. Default is 2048. <para>闪存页面大小。默认为 2048。</param>
    /// <param name="kernel_offset">Kernel offset in memory. Default is 0x00008000. <para>内核内存偏移。默认为 0x00008000。</param>
    /// <param name="ramdisk_offset">Ramdisk offset in memory. Default is 0x01000000. <para>Ramdisk 内存偏移。默认为 0x01000000。</param>
    /// <param name="second_offset">Second stage offset in memory. Default is 0x00F00000. <para>第二阶段内存偏移。默认为 0x00F00000。</param>
    /// <param name="tags_offset">Tags offset in memory. Default is 0x00000100. <para>Tags 内存偏移。默认为 0x00000100。</param>
    /// <param name="dtb_offset">DTB offset in memory. Default is 0x01100000. <para>DTB 内存偏移。默认为 0x01100000。</param>
    /// <param name="os_version">OS version information. Default is 0. <para>操作系统版本信息。默认为 0。</param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse FlashRaw(string partition, string kernelPath, string? ramdiskPath = null, string? secondPath = null, string? dtbPath = null, string? cmdline = null, uint header_version = 0, uint base_addr = 0x10000000, uint page_size = 2048, uint kernel_offset = 0x00008000, uint ramdisk_offset = 0x01000000, uint second_offset = 0x00F00000, uint tags_offset = 0x00000100, uint dtb_offset = 0x01100000, uint os_version = 0)
    {
        FastbootDebug.Log($"FlashRaw(partition={partition}, kernelPath={kernelPath}, ramdiskPath={ramdiskPath}, secondPath={secondPath}, dtbPath={dtbPath}, cmdline={cmdline}, header_version={header_version}, base_addr={base_addr}, page_size={page_size}, kernel_offset={kernel_offset}, ramdisk_offset={ramdisk_offset}, second_offset={second_offset}, tags_offset={tags_offset}, dtb_offset={dtb_offset}, os_version={os_version})");
        byte[] kernel = File.ReadAllBytes(kernelPath);
        byte[]? ramdisk = ramdiskPath != null ? File.ReadAllBytes(ramdiskPath) : null;
        byte[]? second = secondPath != null ? File.ReadAllBytes(secondPath) : null;
        byte[]? dtb = dtbPath != null ? File.ReadAllBytes(dtbPath) : null;

        byte[] bootImg = CreateBootImageVersioned(kernel, ramdisk, second, dtb, cmdline, null, header_version, base_addr, page_size, kernel_offset, ramdisk_offset, second_offset, tags_offset, dtb_offset, os_version);
        using var ms = new MemoryStream(bootImg);
        return FlashRawProtocol(partition, ms, ms.Length);
    }

    /// <summary>
    /// Flashes a raw boot image stream to the specified partition using the fastboot protocol.
    /// Automatically handles slot suffixes for partitioned devices.
    /// <para>使用 fastboot 协议将原始启动镜像流刷写到指定分区。
    /// 自动处理分区设备的槽位后缀。</para>
    /// </summary>
    /// <param name="partition">Target partition to flash (e.g., "boot", "recovery"). <para>目标刷写分区（如 "boot"、"recovery"）。</param>
    /// <param name="stream">Stream containing the boot image data. <para>包含启动镜像数据的流。</param>
    /// <param name="imageSize">Size of the image in bytes. <para>镜像大小（字节）。</param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse FlashRawProtocol(string partition, Stream stream, long imageSize)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (imageSize <= 0 || imageSize > uint.MaxValue)
        {
            return new FastbootResponse
            {
                Result = FastbootState.Fail,
                Response = "invalid image size"
            };
        }

        string targetPartition = partition;
        if (HasSlot(partition))
        {
            targetPartition = partition + "_" + GetCurrentSlot();
        }

        NotifyCurrentStep($"Sending raw boot image to {targetPartition}...");
        var download = DownloadData(stream, imageSize);
        if (download.Result != FastbootState.Success)
        {
            return download;
        }

        NotifyCurrentStep($"Flashing raw boot image to {targetPartition}...");
        return RawCommand("flash:raw:" + targetPartition);
    }
}
