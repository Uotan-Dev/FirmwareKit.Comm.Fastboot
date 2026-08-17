using System.Runtime.InteropServices;
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Creates a boot image with auto-detection of header version based on the version parameter.
    /// Dispatches to the appropriate version-specific method (CreateBootImage0 through CreateBootImage6).
    /// <para>根据 version 参数自动检测头版本创建启动镜像。
    /// 分派到适当的版本特定方法（CreateBootImage0 到 CreateBootImage6）。</para>
    /// </summary>
    private byte[] CreateBootImageVersioned(byte[] kernel, byte[]? ramdisk, byte[]? second, byte[]? dtb, string? cmdline, string? name, uint version, uint baseAddr, uint pageSize, uint kernelOffset, uint ramdiskOffset, uint secondOffset, uint tagsOffset, uint dtbOffset, uint osVersion) => version switch
    {
        0 => CreateBootImage(kernel, ramdisk, second, cmdline, name, baseAddr, pageSize, kernelOffset, ramdiskOffset, secondOffset, tagsOffset, osVersion),
        1 => CreateBootImage1(kernel, ramdisk, second, cmdline, name, baseAddr, pageSize, kernelOffset, ramdiskOffset, secondOffset, tagsOffset, osVersion),
        2 => CreateBootImage2(kernel, ramdisk, second, dtb, cmdline, name, baseAddr, pageSize, kernelOffset, ramdiskOffset, secondOffset, tagsOffset, dtbOffset, osVersion),
        3 => CreateBootImage3(kernel, ramdisk, cmdline, osVersion),
        4 => CreateBootImage4(kernel, ramdisk, cmdline, osVersion),
        5 => CreateBootImage5(kernel, ramdisk, cmdline, osVersion),
        6 => CreateBootImage6(kernel, ramdisk, cmdline, osVersion),
        _ => throw new NotSupportedException($"Boot image header version {version} is not supported for dynamic packaging.")
    };

    /// <summary>
    /// Creates a boot image using header version 0 (legacy Android format).
    /// Includes kernel, optional ramdisk, and optional second stage loader.
    /// <para>使用头版本 0（传统 Android 格式）创建启动镜像。
    /// 包含内核、可选 ramdisk 和可选第二阶段加载器。</para>
    /// </summary>
    /// <param name="kernel">Kernel image data bytes. <para>内核镜像数据字节。</para></param>
    /// <param name="ramdisk">Ramdisk image data bytes (optional). <para>Ramdisk 镜像数据字节（可选）。</para></param>
    /// <param name="second">Second stage loader data bytes (optional). <para>第二阶段加载器数据字节（可选）。</para></param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</para></param>
    /// <param name="name">Image name string (optional). <para>镜像名称字符串（可选）。</para></param>
    /// <param name="baseAddr">Base memory address. Default is 0x10000000. <para>基本内存地址。默认为 0x10000000。</para></param>
    /// <param name="pageSize">Flash page size. Default is 2048. <para>闪存页面大小。默认为 2048。</para></param>
    /// <param name="kernelOffset">Kernel offset in memory. Default is 0x00008000. <para>内核内存偏移。默认为 0x00008000。</para></param>
    /// <param name="ramdiskOffset">Ramdisk offset in memory. Default is 0x01000000. <para>Ramdisk 内存偏移。默认为 0x01000000。</para></param>
    /// <param name="secondOffset">Second stage offset in memory. Default is 0x00F00000. <para>第二阶段内存偏移。默认为 0x00F00000。</para></param>
    /// <param name="tagsOffset">Tags offset in memory. Default is 0x00000100. <para>Tags 内存偏移。默认为 0x00000100。</para></param>
    /// <param name="osVersion">OS version information. Default is 0. <para>操作系统版本信息。默认为 0。</para></param>
    /// <returns>Byte array containing the complete boot image. <para>包含完整启动镜像的字节数组。</para></returns>
    public byte[] CreateBootImage(byte[] kernel, byte[]? ramdisk, byte[]? second, string? cmdline, string? name, uint baseAddr, uint pageSize, uint kernelOffset = 0x00008000, uint ramdiskOffset = 0x01000000, uint secondOffset = 0x00F00000, uint tagsOffset = 0x00000100, uint osVersion = 0)
    {
        var header = BootImageHeaderV0.Create();
        header.KernelSize = (uint)kernel.Length;
        header.KernelAddr = baseAddr + kernelOffset;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.RamdiskAddr = baseAddr + ramdiskOffset;
        header.SecondSize = (uint)(second?.Length ?? 0);
        header.SecondAddr = baseAddr + secondOffset;
        header.TagsAddr = baseAddr + tagsOffset;
        header.PageSize = pageSize;
        header.OsVersion = osVersion;

        CopyToHeaderCmdline(header.Cmdline, cmdline);
        CopyToHeaderName(header.Name, name);

        int headerSize = Marshal.SizeOf<BootImageHeaderV0>();
        return BuildLegacyBootImage(header, kernel, ramdisk, second, headerSize, pageSize);
    }

    /// <summary>
    /// Creates a boot image using header version 1.
    /// Includes kernel, optional ramdisk, and optional second stage loader.
    /// <para>使用头版本 1 创建启动镜像。
    /// 包含内核、可选 ramdisk 和可选第二阶段加载器。</para>
    /// </summary>
    /// <param name="kernel">Kernel image data bytes. <para>内核镜像数据字节。</para></param>
    /// <param name="ramdisk">Ramdisk image data bytes (optional). <para>Ramdisk 镜像数据字节（可选）。</para></param>
    /// <param name="second">Second stage loader data bytes (optional). <para>第二阶段加载器数据字节（可选）。</para></param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</para></param>
    /// <param name="name">Image name string (optional). <para>镜像名称字符串（可选）。</para></param>
    /// <param name="baseAddr">Base memory address. Default is 0x10000000. <para>基本内存地址。默认为 0x10000000。</para></param>
    /// <param name="pageSize">Flash page size. Default is 2048. <para>闪存页面大小。默认为 2048。</para></param>
    /// <param name="kernelOffset">Kernel offset in memory. Default is 0x00008000. <para>内核内存偏移。默认为 0x00008000。</para></param>
    /// <param name="ramdiskOffset">Ramdisk offset in memory. Default is 0x01000000. <para>Ramdisk 内存偏移。默认为 0x01000000。</para></param>
    /// <param name="secondOffset">Second stage offset in memory. Default is 0x00F00000. <para>第二阶段内存偏移。默认为 0x00F00000。</para></param>
    /// <param name="tagsOffset">Tags offset in memory. Default is 0x00000100. <para>Tags 内存偏移。默认为 0x00000100。</para></param>
    /// <param name="osVersion">OS version information. Default is 0. <para>操作系统版本信息。默认为 0。</para></param>
    /// <returns>Byte array containing the complete boot image. <para>包含完整启动镜像的字节数组。</para></returns>
    public byte[] CreateBootImage1(byte[] kernel, byte[]? ramdisk, byte[]? second, string? cmdline, string? name, uint baseAddr, uint pageSize, uint kernelOffset = 0x00008000, uint ramdiskOffset = 0x01000000, uint secondOffset = 0x00F00000, uint tagsOffset = 0x00000100, uint osVersion = 0)
    {
        var header = BootImageHeaderV1.Create();
        header.KernelSize = (uint)kernel.Length;
        header.KernelAddr = baseAddr + kernelOffset;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.RamdiskAddr = baseAddr + ramdiskOffset;
        header.SecondSize = (uint)(second?.Length ?? 0);
        header.SecondAddr = baseAddr + secondOffset;
        header.TagsAddr = baseAddr + tagsOffset;
        header.PageSize = pageSize;
        header.HeaderSize = (uint)Marshal.SizeOf<BootImageHeaderV1>();
        header.OsVersion = osVersion;

        CopyToHeaderCmdline(header.Cmdline, cmdline);
        CopyToHeaderName(header.Name, name);

        return BuildLegacyBootImage(header, kernel, ramdisk, second, (int)header.HeaderSize, pageSize);
    }

    /// <summary>
    /// Creates a boot image using header version 2 (adds DTB support).
    /// Includes kernel, optional ramdisk, optional second stage loader, and optional DTB.
    /// <para>使用头版本 2 创建启动镜像（添加 DTB 支持）。
    /// 包含内核、可选 ramdisk、可选第二阶段加载器和可选 DTB。</para>
    /// </summary>
    /// <param name="kernel">Kernel image data bytes. <para>内核镜像数据字节。</para></param>
    /// <param name="ramdisk">Ramdisk image data bytes (optional). <para>Ramdisk 镜像数据字节（可选）。</para></param>
    /// <param name="second">Second stage loader data bytes (optional). <para>第二阶段加载器数据字节（可选）。</para></param>
    /// <param name="dtb">Device tree blob data bytes (optional). <para>设备树 blob 数据字节（可选）。</para></param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</para></param>
    /// <param name="name">Image name string (optional). <para>镜像名称字符串（可选）。</para></param>
    /// <param name="baseAddr">Base memory address. Default is 0x10000000. <para>基本内存地址。默认为 0x10000000。</para></param>
    /// <param name="pageSize">Flash page size. Default is 2048. <para>闪存页面大小。默认为 2048。</para></param>
    /// <param name="kernelOffset">Kernel offset in memory. Default is 0x00008000. <para>内核内存偏移。默认为 0x00008000。</para></param>
    /// <param name="ramdiskOffset">Ramdisk offset in memory. Default is 0x01000000. <para>Ramdisk 内存偏移。默认为 0x01000000。</para></param>
    /// <param name="secondOffset">Second stage offset in memory. Default is 0x00F00000. <para>第二阶段内存偏移。默认为 0x00F00000。</para></param>
    /// <param name="tagsOffset">Tags offset in memory. Default is 0x00000100. <para>Tags 内存偏移。默认为 0x00000100。</para></param>
    /// <param name="dtbOffset">DTB offset in memory. Default is 0x01100000. <para>DTB 内存偏移。默认为 0x01100000。</para></param>
    /// <param name="osVersion">OS version information. Default is 0. <para>操作系统版本信息。默认为 0。</para></param>
    /// <returns>Byte array containing the complete boot image. <para>包含完整启动镜像的字节数组。</para></returns>
    public byte[] CreateBootImage2(byte[] kernel, byte[]? ramdisk, byte[]? second, byte[]? dtb, string? cmdline, string? name, uint baseAddr, uint pageSize, uint kernelOffset = 0x00008000, uint ramdiskOffset = 0x01000000, uint secondOffset = 0x00F00000, uint tagsOffset = 0x00000100, uint dtbOffset = 0x01100000, uint osVersion = 0)
    {
        var header = BootImageHeaderV2.Create();
        header.KernelSize = (uint)kernel.Length;
        header.KernelAddr = baseAddr + kernelOffset;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.RamdiskAddr = baseAddr + ramdiskOffset;
        header.SecondSize = (uint)(second?.Length ?? 0);
        header.SecondAddr = baseAddr + secondOffset;
        header.TagsAddr = baseAddr + tagsOffset;
        header.DtbSize = (uint)(dtb?.Length ?? 0);
        header.DtbAddr = (ulong)baseAddr + dtbOffset;
        header.PageSize = pageSize;
        header.HeaderSize = (uint)Marshal.SizeOf<BootImageHeaderV2>();
        header.OsVersion = osVersion;

        CopyToHeaderCmdline(header.Cmdline, cmdline);
        CopyToHeaderName(header.Name, name);

        int headerPages = PagesFor((int)header.HeaderSize, pageSize);
        int kernelPages = PagesFor(kernel.Length, pageSize);
        int ramdiskPages = PagesFor(ramdisk?.Length ?? 0, pageSize);
        int secondPages = PagesFor(second?.Length ?? 0, pageSize);
        int dtbPages = PagesFor(dtb?.Length ?? 0, pageSize);

        int totalSize = (headerPages + kernelPages + ramdiskPages + secondPages + dtbPages) * (int)pageSize;
        byte[] buffer = new byte[totalSize];

        var headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(kernel, 0, buffer, headerPages * (int)pageSize, kernel.Length);
        if (ramdisk != null) Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * (int)pageSize, ramdisk.Length);
        if (second != null) Array.Copy(second, 0, buffer, (headerPages + kernelPages + ramdiskPages) * (int)pageSize, second.Length);
        if (dtb != null) Array.Copy(dtb, 0, buffer, (headerPages + kernelPages + ramdiskPages + secondPages) * (int)pageSize, dtb.Length);

        return buffer;
    }

    /// <summary>
    /// Creates a boot image using header version 3 (GKI format, no second stage).
    /// Includes kernel and optional ramdisk. Uses fixed 4096 byte page size.
    /// <para>使用头版本 3 创建启动镜像（GKI 格式，无第二阶段）。
    /// 包含内核和可选 ramdisk。使用固定 4096 字节页面大小。</para>
    /// </summary>
    /// <param name="kernel">Kernel image data bytes. <para>内核镜像数据字节。</para></param>
    /// <param name="ramdisk">Ramdisk image data bytes (optional). <para>Ramdisk 镜像数据字节（可选）。</para></param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</para></param>
    /// <param name="osVersion">OS version information. Default is 0. <para>操作系统版本信息。默认为 0。</para></param>
    /// <returns>Byte array containing the complete boot image. <para>包含完整启动镜像的字节数组。</para></returns>
    public byte[] CreateBootImage3(byte[] kernel, byte[]? ramdisk, string? cmdline, uint osVersion)
    {
        const int pageSize = 4096;
        var header = BootImageHeaderV3.Create();
        header.KernelSize = (uint)kernel.Length;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.OsVersion = osVersion;
        // AOSP sets header_size = sizeof(boot_img_hdr_v3) (= 1580), not the page size.
        header.HeaderSize = (uint)Marshal.SizeOf<BootImageHeaderV3>();
        header.HeaderVersion = 3;

        CopyToHeaderCmdline(header.Cmdline, cmdline, 1536);

        return BuildV3PlusBootImage(header, kernel, ramdisk, null, null, pageSize);
    }

    /// <summary>
    /// Creates a boot image using header version 4 (adds signature support).
    /// Includes kernel, optional ramdisk, and optional signature. Uses fixed 4096 byte page size.
    /// <para>使用头版本 4 创建启动镜像（添加签名支持）。
    /// 包含内核、可选 ramdisk 和可选签名。使用固定 4096 字节页面大小。</para>
    /// </summary>
    /// <param name="kernel">Kernel image data bytes. <para>内核镜像数据字节。</para></param>
    /// <param name="ramdisk">Ramdisk image data bytes (optional). <para>Ramdisk 镜像数据字节（可选）。</para></param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</para></param>
    /// <param name="osVersion">OS version information. Default is 0. <para>操作系统版本信息。默认为 0。</para></param>
    /// <param name="signature">Signature data bytes (optional). <para>签名数据字节（可选）。</para></param>
    /// <returns>Byte array containing the complete boot image. <para>包含完整启动镜像的字节数组。</para></returns>
    public byte[] CreateBootImage4(byte[] kernel, byte[]? ramdisk, string? cmdline, uint osVersion, byte[]? signature = null)
    {
        const int pageSize = 4096;
        var header = BootImageHeaderV4.Create();
        header.KernelSize = (uint)kernel.Length;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.OsVersion = osVersion;
        // AOSP sets header_size = sizeof(boot_img_hdr_v4) (= 1584), not the page size.
        header.HeaderSize = (uint)Marshal.SizeOf<BootImageHeaderV4>();
        header.HeaderVersion = 4;
        header.SignatureSize = (uint)(signature?.Length ?? 0);

        CopyToHeaderCmdline(header.Cmdline, cmdline, 1536);

        return BuildV3PlusBootImage(header, kernel, ramdisk, signature, null, pageSize);
    }

    /// <summary>
    /// Creates a boot image using header version 5 (adds vendor bootconfig).
    /// Includes kernel, optional ramdisk, optional signature, and optional vendor bootconfig. Uses fixed 4096 byte page size.
    /// <para>使用头版本 5 创建启动镜像（添加 vendor bootconfig）。
    /// 包含内核、可选 ramdisk、可选签名和可选 vendor bootconfig。使用固定 4096 字节页面大小。</para>
    /// </summary>
    /// <param name="kernel">Kernel image data bytes. <para>内核镜像数据字节。</para></param>
    /// <param name="ramdisk">Ramdisk image data bytes (optional). <para>Ramdisk 镜像数据字节（可选）。</para></param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</para></param>
    /// <param name="osVersion">OS version information. Default is 0. <para>操作系统版本信息。默认为 0。</para></param>
    /// <param name="signature">Signature data bytes (optional). <para>签名数据字节（可选）。</para></param>
    /// <param name="bootconfig">Vendor bootconfig data bytes (optional). <para>Vendor bootconfig 数据字节（可选）。</para></param>
    /// <returns>Byte array containing the complete boot image. <para>包含完整启动镜像的字节数组。</para></returns>
    public byte[] CreateBootImage5(byte[] kernel, byte[]? ramdisk, string? cmdline, uint osVersion, byte[]? signature = null, byte[]? bootconfig = null)
    {
        const int pageSize = 4096;
        var header = BootImageHeaderV5.Create();
        header.KernelSize = (uint)kernel.Length;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.OsVersion = osVersion;
        // AOSP sets header_size = sizeof(boot_img_hdr_v5) (= 1588), not the page size.
        header.HeaderSize = (uint)Marshal.SizeOf<BootImageHeaderV5>();
        header.HeaderVersion = 5;
        header.SignatureSize = (uint)(signature?.Length ?? 0);
        header.VendorBootconfigSize = (uint)(bootconfig?.Length ?? 0);

        CopyToHeaderCmdline(header.Cmdline, cmdline, 1536);

        return BuildV3PlusBootImage(header, kernel, ramdisk, signature, bootconfig, pageSize);
    }

    /// <summary>
    /// Creates a boot image using header version 6 (latest, adds reserved bytes).
    /// Includes kernel, optional ramdisk, optional signature, and optional vendor bootconfig. Uses fixed 4096 byte page size.
    /// <para>使用头版本 6 创建启动镜像（最新，添加保留字节）。
    /// 包含内核、可选 ramdisk、可选签名和可选 vendor bootconfig。使用固定 4096 字节页面大小。</para>
    /// </summary>
    /// <param name="kernel">Kernel image data bytes. <para>内核镜像数据字节。</para></param>
    /// <param name="ramdisk">Ramdisk image data bytes (optional). <para>Ramdisk 镜像数据字节（可选）。</para></param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</para></param>
    /// <param name="osVersion">OS version information. Default is 0. <para>操作系统版本信息。默认为 0。</para></param>
    /// <param name="signature">Signature data bytes (optional). <para>签名数据字节（可选）。</para></param>
    /// <param name="bootconfig">Vendor bootconfig data bytes (optional). <para>Vendor bootconfig 数据字节（可选）。</para></param>
    /// <returns>Byte array containing the complete boot image. <para>包含完整启动镜像的字节数组。</para></returns>
    public byte[] CreateBootImage6(byte[] kernel, byte[]? ramdisk, string? cmdline, uint osVersion, byte[]? signature = null, byte[]? bootconfig = null)
    {
        const int pageSize = 4096;
        var header = BootImageHeaderV6.Create();
        header.KernelSize = (uint)kernel.Length;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.OsVersion = osVersion;
        // AOSP sets header_size = sizeof(boot_img_hdr_v6) (= 1604), not the page size.
        header.HeaderSize = (uint)Marshal.SizeOf<BootImageHeaderV6>();
        header.HeaderVersion = 6;
        header.SignatureSize = (uint)(signature?.Length ?? 0);
        header.VendorBootconfigSize = (uint)(bootconfig?.Length ?? 0);

        CopyToHeaderCmdline(header.Cmdline, cmdline, 1536);

        return BuildV3PlusBootImage(header, kernel, ramdisk, signature, bootconfig, pageSize);
    }

    /// <summary>
    /// Creates a vendor boot image using header version 3.
    /// Includes vendor ramdisk and DTB. Used for devices with vendor boot partitions.
    /// <para>使用头版本 3 创建 vendor 启动镜像。
    /// 包含 vendor ramdisk 和 DTB。用于具有 vendor boot 分区的设备。</para>
    /// </summary>
    /// <param name="ramdisk">Vendor ramdisk data bytes. <para>Vendor ramdisk 数据字节。</para></param>
    /// <param name="dtb">Device tree blob data bytes. <para>设备树 blob 数据字节。</para></param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</para></param>
    /// <param name="productName">Product name string (optional). <para>产品名称字符串（可选）。</para></param>
    /// <param name="pageSize">Flash page size. Default is 4096. <para>闪存页面大小。默认为 4096。</para></param>
    /// <param name="baseAddr">Base memory address. Default is 0x10000000. <para>基本内存地址。默认为 0x10000000。</para></param>
    /// <returns>Byte array containing the complete vendor boot image. <para>包含完整 vendor 启动镜像的字节数组。</para></returns>
    public byte[] CreateVendorBootImage3(byte[] ramdisk, byte[] dtb, string? cmdline, string? productName, uint pageSize = 4096, uint baseAddr = 0x10000000)
    {
        var header = VendorBootImageHeaderV3.Create();
        header.PageSize = pageSize;
        header.KernelAddr = baseAddr + 0x00008000;
        header.RamdiskAddr = baseAddr + 0x01000000;
        header.TagsAddr = baseAddr + 0x00000100;
        header.VendorRamdiskSize = (uint)ramdisk.Length;
        header.DtbSize = (uint)dtb.Length;
        header.DtbAddr = (ulong)baseAddr + 0x01100000;
        header.HeaderSize = (uint)Marshal.SizeOf<VendorBootImageHeaderV3>();

        CopyToHeaderCmdline(header.Cmdline, cmdline, 2048);
        CopyToHeaderName(header.Name, productName);

        int headerPages = PagesFor((int)header.HeaderSize, pageSize);
        int ramdiskPages = PagesFor(ramdisk.Length, pageSize);
        int dtbPages = PagesFor(dtb.Length, pageSize);

        int totalSize = (headerPages + ramdiskPages + dtbPages) * (int)pageSize;
        byte[] buffer = new byte[totalSize];

        var headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(ramdisk, 0, buffer, headerPages * (int)pageSize, ramdisk.Length);
        Array.Copy(dtb, 0, buffer, (headerPages + ramdiskPages) * (int)pageSize, dtb.Length);

        return buffer;
    }

    /// <summary>
    /// Creates a vendor boot image using header version 4 (adds vendor ramdisk table and bootconfig).
    /// Includes vendor ramdisk, DTB, and optional bootconfig. Used for devices with vendor boot partitions.
    /// <para>使用头版本 4 创建 vendor 启动镜像（添加 vendor ramdisk 表和 bootconfig）。
    /// 包含 vendor ramdisk、DTB 和可选 bootconfig。用于具有 vendor boot 分区的设备。</para>
    /// </summary>
    /// <param name="ramdisk">Vendor ramdisk data bytes. <para>Vendor ramdisk 数据字节。</para></param>
    /// <param name="dtb">Device tree blob data bytes. <para>设备树 blob 数据字节。</para></param>
    /// <param name="cmdline">Kernel command line string (optional). <para>内核命令行字符串（可选）。</para></param>
    /// <param name="productName">Product name string (optional). <para>产品名称字符串（可选）。</para></param>
    /// <param name="bootconfig">Vendor bootconfig data bytes (optional). <para>Vendor bootconfig 数据字节（可选）。</para></param>
    /// <param name="pageSize">Flash page size. Default is 4096. <para>闪存页面大小。默认为 4096。</para></param>
    /// <param name="baseAddr">Base memory address. Default is 0x10000000. <para>基本内存地址。默认为 0x10000000。</para></param>
    /// <returns>Byte array containing the complete vendor boot image. <para>包含完整 vendor 启动镜像的字节数组。</para></returns>
    public byte[] CreateVendorBootImage4(byte[] ramdisk, byte[] dtb, string? cmdline, string? productName, byte[]? bootconfig = null, uint pageSize = 4096, uint baseAddr = 0x10000000)
    {
        var header = VendorBootImageHeaderV4.Create();
        header.PageSize = pageSize;
        header.KernelAddr = baseAddr + 0x00008000;
        header.RamdiskAddr = baseAddr + 0x01000000;
        header.TagsAddr = baseAddr + 0x00000100;
        header.VendorRamdiskSize = (uint)ramdisk.Length;
        header.DtbSize = (uint)dtb.Length;
        header.DtbAddr = (ulong)baseAddr + 0x01100000;
        header.HeaderSize = (uint)Marshal.SizeOf<VendorBootImageHeaderV4>();
        header.BootconfigSize = (uint)(bootconfig?.Length ?? 0);

        CopyToHeaderCmdline(header.Cmdline, cmdline, 2048);
        CopyToHeaderName(header.Name, productName);

        int headerPages = PagesFor((int)header.HeaderSize, pageSize);
        int ramdiskPages = PagesFor(ramdisk.Length, pageSize);
        int dtbPages = PagesFor(dtb.Length, pageSize);
        int configPages = PagesFor((int)header.BootconfigSize, pageSize);

        int totalSize = (headerPages + ramdiskPages + dtbPages + configPages) * (int)pageSize;
        byte[] buffer = new byte[totalSize];

        var headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(ramdisk, 0, buffer, headerPages * (int)pageSize, ramdisk.Length);
        Array.Copy(dtb, 0, buffer, (headerPages + ramdiskPages) * (int)pageSize, dtb.Length);
        if (bootconfig != null)
            Array.Copy(bootconfig, 0, buffer, (headerPages + ramdiskPages + dtbPages) * (int)pageSize, bootconfig.Length);

        return buffer;
    }

    private static int PagesFor(int size, uint pageSize) => (size + (int)pageSize - 1) / (int)pageSize;
    private static int PagesFor(long size, uint pageSize) => ((int)size + (int)pageSize - 1) / (int)pageSize;

    private static void CopyToHeaderCmdline(byte[] headerCmdline, string? cmdline, int maxLen = 512)
    {
        if (string.IsNullOrEmpty(cmdline)) return;
        var cmdBytes = Encoding.ASCII.GetBytes(cmdline);
        Array.Copy(cmdBytes, headerCmdline, Math.Min(cmdBytes.Length, maxLen));
    }

    private static void CopyToHeaderName(byte[] headerName, string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var nameBytes = Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, headerName, Math.Min(nameBytes.Length, headerName.Length));
    }

    private static byte[] BuildLegacyBootImage<THeader>(THeader header, byte[] kernel, byte[]? ramdisk, byte[]? second, int headerSize, uint pageSize) where THeader : struct
    {
        int headerPages = PagesFor(headerSize, pageSize);
        int kernelPages = PagesFor(kernel.Length, pageSize);
        int ramdiskPages = PagesFor(ramdisk?.Length ?? 0, pageSize);
        int secondPages = PagesFor(second?.Length ?? 0, pageSize);

        int totalSize = (headerPages + kernelPages + ramdiskPages + secondPages) * (int)pageSize;
        byte[] buffer = new byte[totalSize];

        var headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(kernel, 0, buffer, headerPages * (int)pageSize, kernel.Length);
        if (ramdisk != null) Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * (int)pageSize, ramdisk.Length);
        if (second != null) Array.Copy(second, 0, buffer, (headerPages + kernelPages + ramdiskPages) * (int)pageSize, second.Length);

        return buffer;
    }

    private static byte[] BuildV3PlusBootImage<THeader>(THeader header, byte[] kernel, byte[]? ramdisk, byte[]? signature, byte[]? bootconfig, int pageSize) where THeader : struct
    {
        uint sigSize = header switch
        {
            BootImageHeaderV4 h4 => h4.SignatureSize,
            BootImageHeaderV5 h5 => h5.SignatureSize,
            BootImageHeaderV6 h6 => h6.SignatureSize,
            _ => 0
        };

        uint configSize = header switch
        {
            BootImageHeaderV5 h5 => h5.VendorBootconfigSize,
            BootImageHeaderV6 h6 => h6.VendorBootconfigSize,
            _ => 0
        };

        int headerPages = PagesFor(pageSize, (uint)pageSize);
        int kernelPages = PagesFor(kernel.Length, (uint)pageSize);
        int ramdiskPages = PagesFor(ramdisk?.Length ?? 0, (uint)pageSize);
        int sigPages = PagesFor((int)sigSize, (uint)pageSize);
        int configPages = PagesFor((int)configSize, (uint)pageSize);

        int totalSize = (headerPages + kernelPages + ramdiskPages + sigPages + configPages) * pageSize;
        byte[] buffer = new byte[totalSize];

        var headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(kernel, 0, buffer, headerPages * pageSize, kernel.Length);
        if (ramdisk != null) Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * pageSize, ramdisk.Length);
        if (signature != null) Array.Copy(signature, 0, buffer, (headerPages + kernelPages + ramdiskPages) * pageSize, signature.Length);
        if (bootconfig != null) Array.Copy(bootconfig, 0, buffer, (headerPages + kernelPages + ramdiskPages + sigPages) * pageSize, bootconfig.Length);

        return buffer;
    }
}
