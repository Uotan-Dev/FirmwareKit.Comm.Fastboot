namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Creates a boot image from files and boots the device.
    /// Builds a boot image from kernel, optional ramdisk, second stage, and DTB files, then boots it.
    /// <para>从文件创建启动镜像并启动设备。
    /// 从内核、可选 ramdisk、第二阶段和 DTB 文件构建启动镜像，然后启动。</para>
    /// </summary>
    /// <param name="kernelPath">Path to the kernel image file. <para>内核镜像文件路径。</para></param>
    /// <param name="ramdiskPath">Path to the ramdisk image file (optional). <para>ramdisk 镜像文件路径（可选）。</para></param>
    /// <param name="secondPath">Path to the second stage loader image file (optional). <para>第二阶段加载器镜像文件路径（可选）。</para></param>
    /// <param name="dtbPath">Path to the DTB image file (optional). <para>DTB 镜像文件路径（可选）。</para></param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</para></param>
    /// <param name="headerVersion">Boot image header version (0-6). Default is 0. <para>启动镜像头版本（0-6）。默认为 0。</para></param>
    /// <param name="baseAddr">Base memory address. Default is 0x10000000. <para>基本内存地址。默认为 0x10000000。</para></param>
    /// <param name="pageSize">Flash page size. Default is 2048. <para>闪存页面大小。默认为 2048。</para></param>
    /// <param name="kernelOffset">Kernel offset in memory. Default is 0x00008000. <para>内核内存偏移。默认为 0x00008000。</para></param>
    /// <param name="ramdiskOffset">Ramdisk offset in memory. Default is 0x01000000. <para>Ramdisk 内存偏移。默认为 0x01000000。</para></param>
    /// <param name="secondOffset">Second stage offset in memory. Default is 0x00F00000. <para>第二阶段内存偏移。默认为 0x00F00000。</para></param>
    /// <param name="tagsOffset">Tags offset in memory. Default is 0x00000100. <para>Tags 内存偏移。默认为 0x00000100。</para></param>
    /// <param name="dtbOffset">DTB offset in memory. Default is 0x01100000. <para>DTB 内存偏移。默认为 0x01100000。</para></param>
    /// <param name="osVersion">OS version information. Default is 0. <para>操作系统版本信息。默认为 0。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Boot(string kernelPath, string? ramdiskPath = null, string? secondPath = null, string? dtbPath = null, string? cmdline = null, uint headerVersion = 0, uint baseAddr = 0x10000000, uint pageSize = 2048, uint kernelOffset = 0x00008000, uint ramdiskOffset = 0x01000000, uint secondOffset = 0x00F00000, uint tagsOffset = 0x00000100, uint dtbOffset = 0x01100000, uint osVersion = 0)
    {
        byte[] kernel = File.ReadAllBytes(kernelPath);
        byte[]? ramdisk = ramdiskPath != null ? File.ReadAllBytes(ramdiskPath) : null;
        byte[]? second = secondPath != null ? File.ReadAllBytes(secondPath) : null;
        byte[]? dtb = dtbPath != null ? File.ReadAllBytes(dtbPath) : null;

        byte[] bootImg = CreateBootImageVersioned(kernel, ramdisk, second, dtb, cmdline, null, headerVersion, baseAddr, pageSize, kernelOffset, ramdiskOffset, secondOffset, tagsOffset, dtbOffset, osVersion);
        return Boot(bootImg);
    }
}
