
using System.Runtime.InteropServices;
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// Android boot image header version 0 (legacy format).
/// <para>Android 启动镜像头版本 0（传统格式）。</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV0
{
    /// <summary>Boot image magic bytes ("ANDROID!" or "VNDRBOOT"). <para>启动镜像魔数（"ANDROID!" 或 "VNDRBOOT"）。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    /// <summary>Kernel image size in bytes. <para>内核镜像大小（字节）。</para></summary>
    public uint KernelSize;
    /// <summary>Kernel load address. <para>内核加载地址。</para></summary>
    public uint KernelAddr;

    /// <summary>Ramdisk image size in bytes. <para>Ramdisk 镜像大小（字节）。</para></summary>
    public uint RamdiskSize;
    /// <summary>Ramdisk load address. <para>Ramdisk 加载地址。</para></summary>
    public uint RamdiskAddr;

    /// <summary>Second stage loader size in bytes. <para>第二阶段加载器大小（字节）。</para></summary>
    public uint SecondSize;
    /// <summary>Second stage loader load address. <para>第二阶段加载器加载地址。</para></summary>
    public uint SecondAddr;

    /// <summary>Tags load address. <para>Tags 加载地址。</para></summary>
    public uint TagsAddr;
    /// <summary>Flash page size in bytes. <para>闪存页大小（字节）。</para></summary>
    public uint PageSize;

    /// <summary>Boot image header version. <para>启动镜像头版本。</para></summary>
    public uint HeaderVersion;

    /// <summary>OS version encoded as a single integer. <para>编码为单个整数的 OS 版本。</para></summary>
    public uint OsVersion;

    /// <summary>Product name string. <para>产品名称字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Name;

    /// <summary>Kernel command line string. <para>内核命令行字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
    public byte[] Cmdline;

    /// <summary>SHA-1 digest IDs of the image components. <para>镜像组件的 SHA-1 摘要 ID。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public uint[] Id;

    /// <summary>Extra command line data. <para>额外命令行数据。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
    public byte[] ExtraCmdline;

    /// <summary>
    /// Creates a new BootImageHeaderV0 with default ANDROID! magic.
    /// <para>创建一个带有默认 ANDROID! 魔数的 BootImageHeaderV0。</para>
    /// </summary>
    public static BootImageHeaderV0 Create()
    {
        return new BootImageHeaderV0
        {
            Magic = Encoding.ASCII.GetBytes("ANDROID!"),
            Name = new byte[16],
            Cmdline = new byte[512],
            Id = new uint[8],
            ExtraCmdline = new byte[1024]
        };
    }
}

/// <summary>
/// Android boot image header version 1.
/// <para>Android 启动镜像头版本 1。</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV1
{
    /// <summary>Boot image magic bytes ("ANDROID!" or "VNDRBOOT"). <para>启动镜像魔数（"ANDROID!" 或 "VNDRBOOT"）。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    /// <summary>Kernel image size in bytes. <para>内核镜像大小（字节）。</para></summary>
    public uint KernelSize;
    /// <summary>Kernel load address. <para>内核加载地址。</para></summary>
    public uint KernelAddr;

    /// <summary>Ramdisk image size in bytes. <para>Ramdisk 镜像大小（字节）。</para></summary>
    public uint RamdiskSize;
    /// <summary>Ramdisk load address. <para>Ramdisk 加载地址。</para></summary>
    public uint RamdiskAddr;

    /// <summary>Second stage loader size in bytes. <para>第二阶段加载器大小（字节）。</para></summary>
    public uint SecondSize;
    /// <summary>Second stage loader load address. <para>第二阶段加载器加载地址。</para></summary>
    public uint SecondAddr;

    /// <summary>Tags load address. <para>Tags 加载地址。</para></summary>
    public uint TagsAddr;
    /// <summary>Flash page size in bytes. <para>闪存页大小（字节）。</para></summary>
    public uint PageSize;

    /// <summary>Boot image header version. <para>启动镜像头版本。</para></summary>
    public uint HeaderVersion;

    /// <summary>OS version encoded as a single integer. <para>编码为单个整数的 OS 版本。</para></summary>
    public uint OsVersion;

    /// <summary>Product name string. <para>产品名称字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Name;

    /// <summary>Kernel command line string. <para>内核命令行字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
    public byte[] Cmdline;

    /// <summary>SHA-1 digest IDs of the image components. <para>镜像组件的 SHA-1 摘要 ID。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public uint[] Id;

    /// <summary>Extra command line data. <para>额外命令行数据。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
    public byte[] ExtraCmdline;

    /// <summary>Recovery DTB image size in bytes. <para>Recovery DTB 镜像大小（字节）。</para></summary>
    public uint RecoveryDtboSize;
    /// <summary>Recovery DTB image offset in bytes. <para>Recovery DTB 镜像偏移（字节）。</para></summary>
    public ulong RecoveryDtboOffset;

    /// <summary>Header size in bytes. <para>头大小（字节）。</para></summary>
    public uint HeaderSize;

    /// <summary>
    /// Creates a new BootImageHeaderV1 with default values.
    /// <para>创建一个带有默认值的 BootImageHeaderV1。</para>
    /// </summary>
    public static BootImageHeaderV1 Create()
    {
        return new BootImageHeaderV1
        {
            Magic = Encoding.ASCII.GetBytes("ANDROID!"),
            HeaderVersion = 1,
            Name = new byte[16],
            Cmdline = new byte[512],
            Id = new uint[8],
            ExtraCmdline = new byte[1024],
            HeaderSize = (uint)Marshal.SizeOf<BootImageHeaderV1>()
        };
    }
}

/// <summary>
/// Android boot image header version 2 (adds DTB support).
/// <para>Android 启动镜像头版本 2（添加 DTB 支持）。</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV2
{
    /// <summary>Boot image magic bytes ("ANDROID!" or "VNDRBOOT"). <para>启动镜像魔数（"ANDROID!" 或 "VNDRBOOT"）。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    /// <summary>Kernel image size in bytes. <para>内核镜像大小（字节）。</para></summary>
    public uint KernelSize;
    /// <summary>Kernel load address. <para>内核加载地址。</para></summary>
    public uint KernelAddr;

    /// <summary>Ramdisk image size in bytes. <para>Ramdisk 镜像大小（字节）。</para></summary>
    public uint RamdiskSize;
    /// <summary>Ramdisk load address. <para>Ramdisk 加载地址。</para></summary>
    public uint RamdiskAddr;

    /// <summary>Second stage loader size in bytes. <para>第二阶段加载器大小（字节）。</para></summary>
    public uint SecondSize;
    /// <summary>Second stage loader load address. <para>第二阶段加载器加载地址。</para></summary>
    public uint SecondAddr;

    /// <summary>Tags load address. <para>Tags 加载地址。</para></summary>
    public uint TagsAddr;
    /// <summary>Flash page size in bytes. <para>闪存页大小（字节）。</para></summary>
    public uint PageSize;

    /// <summary>Boot image header version. <para>启动镜像头版本。</para></summary>
    public uint HeaderVersion;

    /// <summary>OS version encoded as a single integer. <para>编码为单个整数的 OS 版本。</para></summary>
    public uint OsVersion;

    /// <summary>Product name string. <para>产品名称字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Name;

    /// <summary>Kernel command line string. <para>内核命令行字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
    public byte[] Cmdline;

    /// <summary>SHA-1 digest IDs of the image components. <para>镜像组件的 SHA-1 摘要 ID。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public uint[] Id;

    /// <summary>Extra command line data. <para>额外命令行数据。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
    public byte[] ExtraCmdline;

    /// <summary>Recovery DTB image size in bytes. <para>Recovery DTB 镜像大小（字节）。</para></summary>
    public uint RecoveryDtboSize;
    /// <summary>Recovery DTB image offset in bytes. <para>Recovery DTB 镜像偏移（字节）。</para></summary>
    public ulong RecoveryDtboOffset;

    /// <summary>Header size in bytes. <para>头大小（字节）。</para></summary>
    public uint HeaderSize;

    /// <summary>Device tree blob size in bytes. <para>设备树 blob 大小（字节）。</para></summary>
    public uint DtbSize;
    /// <summary>Device tree blob load address. <para>设备树 blob 加载地址。</para></summary>
    public ulong DtbAddr;

    // AOSP boot_img_hdr_v2 is 1664 bytes (4 reserved bytes follow dtb_addr).
    /// <summary>Reserved header bytes (4 bytes after dtb_addr in v2). <para>保留头字节（v2 中 dtb_addr 后的 4 字节）。</para></summary>
    public uint Reserved;

    /// <summary>
    /// Creates a new BootImageHeaderV2 with default values.
    /// <para>创建一个带有默认值的 BootImageHeaderV2。</para>
    /// </summary>
    public static BootImageHeaderV2 Create()
    {
        return new BootImageHeaderV2
        {
            Magic = Encoding.ASCII.GetBytes("ANDROID!"),
            HeaderVersion = 2,
            Name = new byte[16],
            Cmdline = new byte[512],
            Id = new uint[8],
            ExtraCmdline = new byte[1024],
            HeaderSize = (uint)Marshal.SizeOf<BootImageHeaderV2>()
        };
    }
}

/// <summary>
/// Android boot image header version 3 (GKI format, no second stage).
/// <para>Android 启动镜像头版本 3（GKI 格式，无第二阶段）。</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV3
{
    /// <summary>Boot image magic bytes ("ANDROID!" or "VNDRBOOT"). <para>启动镜像魔数（"ANDROID!" 或 "VNDRBOOT"）。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    /// <summary>Kernel image size in bytes. <para>内核镜像大小（字节）。</para></summary>
    public uint KernelSize;
    /// <summary>Ramdisk image size in bytes. <para>Ramdisk 镜像大小（字节）。</para></summary>
    public uint RamdiskSize;
    /// <summary>OS version encoded as a single integer. <para>编码为单个整数的 OS 版本。</para></summary>
    public uint OsVersion;
    /// <summary>Header size in bytes. <para>头大小（字节）。</para></summary>
    public uint HeaderSize;

    /// <summary>Reserved header words. <para>保留头字。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public uint[] Reserved;

    /// <summary>Boot image header version. <para>启动镜像头版本。</para></summary>
    public uint HeaderVersion;

    /// <summary>Kernel command line string. <para>内核命令行字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1536)]
    public byte[] Cmdline;

    /// <summary>
    /// Creates a new BootImageHeaderV3 with default values.
    /// <para>创建一个带有默认值的 BootImageHeaderV3。</para>
    /// </summary>
    public static BootImageHeaderV3 Create()
    {
        return new BootImageHeaderV3
        {
            Magic = Encoding.ASCII.GetBytes("ANDROID!"),
            Reserved = new uint[4],
            HeaderVersion = 3,
            Cmdline = new byte[1536]
        };
    }
}

/// <summary>
/// Android boot image header version 4 (adds signature support).
/// <para>Android 启动镜像头版本 4（添加签名支持）。</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV4
{
    /// <summary>Boot image magic bytes ("ANDROID!" or "VNDRBOOT"). <para>启动镜像魔数（"ANDROID!" 或 "VNDRBOOT"）。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    /// <summary>Kernel image size in bytes. <para>内核镜像大小（字节）。</para></summary>
    public uint KernelSize;
    /// <summary>Ramdisk image size in bytes. <para>Ramdisk 镜像大小（字节）。</para></summary>
    public uint RamdiskSize;
    /// <summary>OS version encoded as a single integer. <para>编码为单个整数的 OS 版本。</para></summary>
    public uint OsVersion;
    /// <summary>Header size in bytes. <para>头大小（字节）。</para></summary>
    public uint HeaderSize;

    /// <summary>Reserved header words. <para>保留头字。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public uint[] Reserved;

    /// <summary>Boot image header version. <para>启动镜像头版本。</para></summary>
    public uint HeaderVersion;

    /// <summary>Kernel command line string. <para>内核命令行字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1536)]
    public byte[] Cmdline;

    /// <summary>Signature size in bytes (v4+). <para>签名大小（字节，v4+）。</para></summary>
    public uint SignatureSize;

    /// <summary>
    /// Creates a new BootImageHeaderV4 with default values.
    /// <para>创建一个带有默认值的 BootImageHeaderV4。</para>
    /// </summary>
    public static BootImageHeaderV4 Create()
    {
        return new BootImageHeaderV4
        {
            Magic = Encoding.ASCII.GetBytes("ANDROID!"),
            Reserved = new uint[4],
            HeaderVersion = 4,
            Cmdline = new byte[1536]
        };
    }
}

/// <summary>
/// Android boot image header version 5 (adds vendor bootconfig).
/// <para>Android 启动镜像头版本 5（添加 vendor bootconfig）。</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV5
{
    /// <summary>Boot image magic bytes ("ANDROID!" or "VNDRBOOT"). <para>启动镜像魔数（"ANDROID!" 或 "VNDRBOOT"）。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    /// <summary>Kernel image size in bytes. <para>内核镜像大小（字节）。</para></summary>
    public uint KernelSize;
    /// <summary>Ramdisk image size in bytes. <para>Ramdisk 镜像大小（字节）。</para></summary>
    public uint RamdiskSize;
    /// <summary>OS version encoded as a single integer. <para>编码为单个整数的 OS 版本。</para></summary>
    public uint OsVersion;
    /// <summary>Header size in bytes. <para>头大小（字节）。</para></summary>
    public uint HeaderSize;

    /// <summary>Reserved header words. <para>保留头字。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public uint[] Reserved;

    /// <summary>Boot image header version. <para>启动镜像头版本。</para></summary>
    public uint HeaderVersion;

    /// <summary>Kernel command line string. <para>内核命令行字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1536)]
    public byte[] Cmdline;

    /// <summary>Signature size in bytes (v4+). <para>签名大小（字节，v4+）。</para></summary>
    public uint SignatureSize;
    /// <summary>Vendor bootconfig size in bytes (v5+). <para>Vendor bootconfig 大小（字节，v5+）。</para></summary>
    public uint VendorBootconfigSize;

    /// <summary>
    /// Creates a new BootImageHeaderV5 with default values.
    /// <para>创建一个带有默认值的 BootImageHeaderV5。</para>
    /// </summary>
    public static BootImageHeaderV5 Create()
    {
        return new BootImageHeaderV5
        {
            Magic = Encoding.ASCII.GetBytes("ANDROID!"),
            Reserved = new uint[4],
            HeaderVersion = 5,
            Cmdline = new byte[1536]
        };
    }
}

/// <summary>
/// Android boot image header version 6 (latest, adds reserved bytes).
/// <para>Android 启动镜像头版本 6（最新，添加保留字节）。</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV6
{
    /// <summary>Boot image magic bytes ("ANDROID!" or "VNDRBOOT"). <para>启动镜像魔数（"ANDROID!" 或 "VNDRBOOT"）。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    /// <summary>Kernel image size in bytes. <para>内核镜像大小（字节）。</para></summary>
    public uint KernelSize;
    /// <summary>Ramdisk image size in bytes. <para>Ramdisk 镜像大小（字节）。</para></summary>
    public uint RamdiskSize;
    /// <summary>OS version encoded as a single integer. <para>编码为单个整数的 OS 版本。</para></summary>
    public uint OsVersion;
    /// <summary>Header size in bytes. <para>头大小（字节）。</para></summary>
    public uint HeaderSize;

    /// <summary>Reserved header words. <para>保留头字。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public uint[] Reserved;

    /// <summary>Boot image header version. <para>启动镜像头版本。</para></summary>
    public uint HeaderVersion;

    /// <summary>Kernel command line string. <para>内核命令行字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1536)]
    public byte[] Cmdline;

    /// <summary>Signature size in bytes (v4+). <para>签名大小（字节，v4+）。</para></summary>
    public uint SignatureSize;
    /// <summary>Vendor bootconfig size in bytes (v5+). <para>Vendor bootconfig 大小（字节，v5+）。</para></summary>
    public uint VendorBootconfigSize;

    /// <summary>Additional reserved header bytes (v6). <para>额外保留头字节（v6）。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Reserved1;

    /// <summary>
    /// Creates a new BootImageHeaderV6 with default values.
    /// <para>创建一个带有默认值的 BootImageHeaderV6。</para>
    /// </summary>
    public static BootImageHeaderV6 Create()
    {
        return new BootImageHeaderV6
        {
            Magic = Encoding.ASCII.GetBytes("ANDROID!"),
            Reserved = new uint[4],
            HeaderVersion = 6,
            Cmdline = new byte[1536]
        };
    }
}

/// <summary>
/// Vendor boot image header version 3.
/// <para>Vendor 启动镜像头版本 3。</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VendorBootImageHeaderV3
{
    /// <summary>Boot image magic bytes ("ANDROID!" or "VNDRBOOT"). <para>启动镜像魔数（"ANDROID!" 或 "VNDRBOOT"）。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    /// <summary>Boot image header version. <para>启动镜像头版本。</para></summary>
    public uint HeaderVersion;
    /// <summary>Flash page size in bytes. <para>闪存页大小（字节）。</para></summary>
    public uint PageSize;
    /// <summary>Kernel load address. <para>内核加载地址。</para></summary>
    public uint KernelAddr;
    /// <summary>Ramdisk load address. <para>Ramdisk 加载地址。</para></summary>
    public uint RamdiskAddr;
    /// <summary>Vendor ramdisk size in bytes. <para>Vendor ramdisk 大小（字节）。</para></summary>
    public uint VendorRamdiskSize;

    /// <summary>Kernel command line string. <para>内核命令行字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)]
    public byte[] Cmdline;

    /// <summary>Tags load address. <para>Tags 加载地址。</para></summary>
    public uint TagsAddr;

    /// <summary>Product name string. <para>产品名称字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Name;

    /// <summary>Header size in bytes. <para>头大小（字节）。</para></summary>
    public uint HeaderSize;
    /// <summary>Device tree blob size in bytes. <para>设备树 blob 大小（字节）。</para></summary>
    public uint DtbSize;
    /// <summary>Device tree blob load address. <para>设备树 blob 加载地址。</para></summary>
    public ulong DtbAddr;

    /// <summary>
    /// Creates a new VendorBootImageHeaderV3 with default values.
    /// <para>创建一个带有默认值的 VendorBootImageHeaderV3。</para>
    /// </summary>
    public static VendorBootImageHeaderV3 Create()
    {
        return new VendorBootImageHeaderV3
        {
            Magic = Encoding.ASCII.GetBytes("VNDRBOOT"),
            HeaderVersion = 3,
            Cmdline = new byte[2048],
            Name = new byte[16]
        };
    }
}

/// <summary>
/// Vendor boot image header version 4 (adds vendor ramdisk table and bootconfig).
/// <para>Vendor 启动镜像头版本 4（添加 vendor ramdisk 表和 bootconfig）。</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VendorBootImageHeaderV4
{
    /// <summary>Boot image magic bytes ("ANDROID!" or "VNDRBOOT"). <para>启动镜像魔数（"ANDROID!" 或 "VNDRBOOT"）。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    /// <summary>Boot image header version. <para>启动镜像头版本。</para></summary>
    public uint HeaderVersion;
    /// <summary>Flash page size in bytes. <para>闪存页大小（字节）。</para></summary>
    public uint PageSize;
    /// <summary>Kernel load address. <para>内核加载地址。</para></summary>
    public uint KernelAddr;
    /// <summary>Ramdisk load address. <para>Ramdisk 加载地址。</para></summary>
    public uint RamdiskAddr;
    /// <summary>Vendor ramdisk size in bytes. <para>Vendor ramdisk 大小（字节）。</para></summary>
    public uint VendorRamdiskSize;

    /// <summary>Kernel command line string. <para>内核命令行字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)]
    public byte[] Cmdline;

    /// <summary>Tags load address. <para>Tags 加载地址。</para></summary>
    public uint TagsAddr;

    /// <summary>Product name string. <para>产品名称字符串。</para></summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Name;

    /// <summary>Header size in bytes. <para>头大小（字节）。</para></summary>
    public uint HeaderSize;
    /// <summary>Device tree blob size in bytes. <para>设备树 blob 大小（字节）。</para></summary>
    public uint DtbSize;
    /// <summary>Device tree blob load address. <para>设备树 blob 加载地址。</para></summary>
    public ulong DtbAddr;

    /// <summary>Vendor ramdisk table size in bytes. <para>Vendor ramdisk 表大小（字节）。</para></summary>
    public uint VendorRamdiskTableSize;
    /// <summary>Number of entries in the vendor ramdisk table. <para>Vendor ramdisk 表条目数。</para></summary>
    public uint VendorRamdiskTableEntryNum;
    /// <summary>Size of each vendor ramdisk table entry. <para>每个 vendor ramdisk 表条目的大小。</para></summary>
    public uint VendorRamdiskTableEntrySize;
    /// <summary>Bootconfig size in bytes. <para>Bootconfig 大小（字节）。</para></summary>
    public uint BootconfigSize;

    /// <summary>
    /// Creates a new VendorBootImageHeaderV4 with default values.
    /// <para>创建一个带有默认值的 VendorBootImageHeaderV4。</para>
    /// </summary>
    public static VendorBootImageHeaderV4 Create()
    {
        return new VendorBootImageHeaderV4
        {
            Magic = Encoding.ASCII.GetBytes("VNDRBOOT"),
            HeaderVersion = 4,
            Cmdline = new byte[2048],
            Name = new byte[16]
        };
    }
}

/// <summary>
/// Represents an Android boot image with header and payload data (kernel, ramdisk, etc.).
/// <para>表示包含头和负载数据（内核、ramdisk 等）的 Android 启动镜像。</para>
/// </summary>
public class BootImage
{
    /// <summary>
    /// Gets or sets the boot image header (version-specific struct).
    /// <para>获取或设置启动镜像头（特定版本的结构体）。</para>
    /// </summary>
    public object Header { get; set; }

    /// <summary>
    /// Gets or sets the kernel data.
    /// <para>获取或设置内核数据。</para>
    /// </summary>
    public byte[] Kernel { get; set; } = [];

    /// <summary>
    /// Gets or sets the ramdisk data.
    /// <para>获取或设置 ramdisk 数据。</para>
    /// </summary>
    public byte[] Ramdisk { get; set; } = [];

    /// <summary>
    /// Gets or sets the second-stage loader data.
    /// <para>获取或设置第二阶段加载器数据。</para>
    /// </summary>
    public byte[] Second { get; set; } = [];

    /// <summary>
    /// Gets or sets the device tree blob (DTB) data.
    /// <para>获取或设置设备树 blob (DTB) 数据。</para>
    /// </summary>
    public byte[] Dtb { get; set; } = [];

    /// <summary>
    /// Gets or sets the signature data.
    /// <para>获取或设置签名数据。</para>
    /// </summary>
    public byte[] Signature { get; set; } = [];

    /// <summary>
    /// Gets or sets the vendor ramdisk table data.
    /// <para>获取或设置 vendor ramdisk 表数据。</para>
    /// </summary>
    public byte[] VendorRamdiskTable { get; set; } = [];

    /// <summary>
    /// Gets or sets the bootconfig data.
    /// <para>获取或设置 bootconfig 数据。</para>
    /// </summary>
    public byte[] Bootconfig { get; set; } = [];

    /// <summary>
    /// Initializes a new BootImage with the specified header.
    /// <para>使用指定的头初始化新的 BootImage。</para>
    /// </summary>
    public BootImage(object header)
    {
        Header = header;
    }

    /// <summary>
    /// Parses a boot image from a stream, auto-detecting the header version.
    /// <para>从流解析启动镜像，自动检测头版本。</para>
    /// </summary>
    public static BootImage Parse(Stream stream)
    {
        byte[] magic = new byte[8];
        ReadStreamFully(stream, magic, 8);
        stream.Seek(-8, SeekOrigin.Current);

        string magicStr = Encoding.ASCII.GetString(magic);
        if (magicStr == "ANDROID!")
        {
            stream.Seek(40, SeekOrigin.Begin);
            byte[] versionBytes = new byte[4];
            ReadStreamFully(stream, versionBytes, 4);
            uint version = BitConverter.ToUInt32(versionBytes, 0);
            stream.Seek(0, SeekOrigin.Begin);

            if (version == 0)
            {
                var header = DataHelper.Deserialize<BootImageHeaderV0>(stream);
                var boot = new BootImage(header);
                boot.ReadData(stream, header);
                return boot;
            }
            else if (version == 1)
            {
                var header = DataHelper.Deserialize<BootImageHeaderV1>(stream);
                var boot = new BootImage(header);
                boot.ReadData(stream, header);
                return boot;
            }
            else if (version == 2)
            {
                var header = DataHelper.Deserialize<BootImageHeaderV2>(stream);
                var boot = new BootImage(header);
                boot.ReadData(stream, header);
                return boot;
            }
            else if (version == 3)
            {
                var header = DataHelper.Deserialize<BootImageHeaderV3>(stream);
                var boot = new BootImage(header);
                boot.ReadData(stream, header);
                return boot;
            }
            else if (version == 4)
            {
                var header = DataHelper.Deserialize<BootImageHeaderV4>(stream);
                var boot = new BootImage(header);
                boot.ReadData(stream, header);
                return boot;
            }
            else if (version == 5)
            {
                var header = DataHelper.Deserialize<BootImageHeaderV5>(stream);
                var boot = new BootImage(header);
                boot.ReadData(stream, header);
                return boot;
            }
            else if (version == 6)
            {
                var header = DataHelper.Deserialize<BootImageHeaderV6>(stream);
                var boot = new BootImage(header);
                boot.ReadData(stream, header);
                return boot;
            }
        }
        else if (magicStr == "VNDRBOOT")
        {
            stream.Seek(8, SeekOrigin.Begin);
            byte[] versionBytes = new byte[4];
            ReadStreamFully(stream, versionBytes, 4);
            uint version = BitConverter.ToUInt32(versionBytes, 0);
            stream.Seek(0, SeekOrigin.Begin);

            if (version == 3)
            {
                var header = DataHelper.Deserialize<VendorBootImageHeaderV3>(stream);
                var boot = new BootImage(header);
                boot.ReadData(stream, header);
                return boot;
            }
            else if (version == 4)
            {
                var header = DataHelper.Deserialize<VendorBootImageHeaderV4>(stream);
                var boot = new BootImage(header);
                boot.ReadData(stream, header);
                return boot;
            }
        }
        throw new NotSupportedException("Unknown boot image magic: " + magicStr);
    }

    private void ReadData(Stream stream, BootImageHeaderV0 header)
    {
        uint pageSize = header.PageSize;
        long offset = pageSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, pageSize);
        offset += (header.KernelSize + pageSize - 1) / pageSize * pageSize;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, pageSize);
        offset += (header.RamdiskSize + pageSize - 1) / pageSize * pageSize;
        Second = ReadPadded(stream, offset, header.SecondSize, pageSize);
    }

    private void ReadData(Stream stream, BootImageHeaderV1 header)
    {
        uint pageSize = header.PageSize;
        // header_size is the struct size (1648 for v1); payloads begin at the next page boundary.
        long offset = (header.HeaderSize + pageSize - 1) / pageSize * pageSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, pageSize);
        offset += (header.KernelSize + pageSize - 1) / pageSize * pageSize;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, pageSize);
        offset += (header.RamdiskSize + pageSize - 1) / pageSize * pageSize;
        Second = ReadPadded(stream, offset, header.SecondSize, pageSize);
    }

    private void ReadData(Stream stream, BootImageHeaderV2 header)
    {
        uint pageSize = header.PageSize;
        // header_size is the struct size (1664 for v2); payloads begin at the next page boundary.
        long offset = (header.HeaderSize + pageSize - 1) / pageSize * pageSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, pageSize);
        offset += (header.KernelSize + pageSize - 1) / pageSize * pageSize;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, pageSize);
        offset += (header.RamdiskSize + pageSize - 1) / pageSize * pageSize;
        Second = ReadPadded(stream, offset, header.SecondSize, pageSize);
    }

    private void ReadData(Stream stream, BootImageHeaderV3 header)
    {
        const int pageSize = 4096;
        long offset = (header.HeaderSize + pageSize - 1) / pageSize * pageSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, pageSize);
        offset += (header.KernelSize + 4095) / 4096 * 4096;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, pageSize);
    }

    private void ReadData(Stream stream, BootImageHeaderV4 header)
    {
        const int pageSize = 4096;
        long offset = (header.HeaderSize + pageSize - 1) / pageSize * pageSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, pageSize);
        offset += (header.KernelSize + 4095) / 4096 * 4096;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, pageSize);
        offset += (header.RamdiskSize + 4095) / 4096 * 4096;
        Signature = ReadPadded(stream, offset, header.SignatureSize, pageSize);
    }

    private void ReadData(Stream stream, BootImageHeaderV5 header)
    {
        const int pageSize = 4096;
        long offset = (header.HeaderSize + pageSize - 1) / pageSize * pageSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, pageSize);
        offset += (header.KernelSize + 4095) / 4096 * 4096;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, pageSize);
        offset += (header.RamdiskSize + 4095) / 4096 * 4096;
        Signature = ReadPadded(stream, offset, header.SignatureSize, pageSize);
        offset += (header.SignatureSize + 4095) / 4096 * 4096;
        Bootconfig = ReadPadded(stream, offset, header.VendorBootconfigSize, pageSize);
    }

    private void ReadData(Stream stream, BootImageHeaderV6 header)
    {
        const int pageSize = 4096;
        long offset = (header.HeaderSize + pageSize - 1) / pageSize * pageSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, pageSize);
        offset += (header.KernelSize + 4095) / 4096 * 4096;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, pageSize);
        offset += (header.RamdiskSize + 4095) / 4096 * 4096;
        Signature = ReadPadded(stream, offset, header.SignatureSize, pageSize);
        offset += (header.SignatureSize + 4095) / 4096 * 4096;
        Bootconfig = ReadPadded(stream, offset, header.VendorBootconfigSize, pageSize);
    }

    private void ReadData(Stream stream, VendorBootImageHeaderV3 header)
    {
        long offset = header.HeaderSize;
        Ramdisk = ReadPadded(stream, offset, header.VendorRamdiskSize, header.PageSize);
        offset += (header.VendorRamdiskSize + header.PageSize - 1) / header.PageSize * header.PageSize;
        Dtb = ReadPadded(stream, offset, header.DtbSize, header.PageSize);
    }

    private void ReadData(Stream stream, VendorBootImageHeaderV4 header)
    {
        long offset = header.HeaderSize;
        Ramdisk = ReadPadded(stream, offset, header.VendorRamdiskSize, header.PageSize);
        offset += (header.VendorRamdiskSize + header.PageSize - 1) / header.PageSize * header.PageSize;
        Dtb = ReadPadded(stream, offset, header.DtbSize, header.PageSize);
        offset += (header.DtbSize + header.PageSize - 1) / header.PageSize * header.PageSize;
        VendorRamdiskTable = ReadPadded(stream, offset, header.VendorRamdiskTableSize, header.PageSize);
        offset += (header.VendorRamdiskTableSize + header.PageSize - 1) / header.PageSize * header.PageSize;
        Bootconfig = ReadPadded(stream, offset, header.BootconfigSize, header.PageSize);
    }

    /// <summary>
    /// Gets the bootconfig content as a text string.
    /// <para>以文本字符串形式获取 bootconfig 内容。</para>
    /// </summary>
    public string GetBootconfigText()
    {
        if (Bootconfig == null || Bootconfig.Length == 0) return "";
        return Encoding.ASCII.GetString(Bootconfig).TrimEnd('\0');
    }

    /// <summary>
    /// Sets the bootconfig content from a text string.
    /// <para>从文本字符串设置 bootconfig 内容。</para>
    /// </summary>
    public void SetBootconfigText(string text)
    {
        Bootconfig = Encoding.ASCII.GetBytes(text + "\0");
    }

    /// <summary>
    /// Adds a key-value pair to the bootconfig.
    /// <para>向 bootconfig 添加键值对。</para>
    /// </summary>
    public void AddBootconfig(string key, string value)
    {
        string current = GetBootconfigText();
        SetBootconfigText(current + (string.IsNullOrEmpty(current) ? "" : "\n") + $"{key} = \"{value}\"");
    }

    /// <summary>
    /// Serializes the boot image to a stream, writing the header and all payload sections.
    /// <para>将启动镜像序列化到流，写入头和所有负载段。</para>
    /// </summary>
    public void Serialize(Stream stream)
    {
        if (Header is BootImageHeaderV0 h0)
        {
            h0.KernelSize = (uint)Kernel.Length;
            h0.RamdiskSize = (uint)Ramdisk.Length;
            h0.SecondSize = (uint)Second.Length;
            DataHelper.Serialize(stream, h0);
            WritePadded(stream, Kernel, h0.PageSize);
            WritePadded(stream, Ramdisk, h0.PageSize);
            WritePadded(stream, Second, h0.PageSize);
        }
        else if (Header is BootImageHeaderV1 h1)
        {
            h1.KernelSize = (uint)Kernel.Length;
            h1.RamdiskSize = (uint)Ramdisk.Length;
            h1.SecondSize = (uint)Second.Length;
            DataHelper.Serialize(stream, h1);
            WritePadded(stream, Kernel, h1.PageSize);
            WritePadded(stream, Ramdisk, h1.PageSize);
            WritePadded(stream, Second, h1.PageSize);
        }
        else if (Header is BootImageHeaderV3 h3)
        {
            h3.KernelSize = (uint)Kernel.Length;
            h3.RamdiskSize = (uint)Ramdisk.Length;
            DataHelper.Serialize(stream, h3);
            WritePadded(stream, Kernel, 4096);
            WritePadded(stream, Ramdisk, 4096);
        }
        else if (Header is BootImageHeaderV4 h4)
        {
            h4.KernelSize = (uint)Kernel.Length;
            h4.RamdiskSize = (uint)Ramdisk.Length;
            h4.SignatureSize = (uint)Signature.Length;
            DataHelper.Serialize(stream, h4);
            WritePadded(stream, Kernel, 4096);
            WritePadded(stream, Ramdisk, 4096);
            WritePadded(stream, Signature, 4096);
        }
        else if (Header is BootImageHeaderV5 h5)
        {
            h5.KernelSize = (uint)Kernel.Length;
            h5.RamdiskSize = (uint)Ramdisk.Length;
            h5.SignatureSize = (uint)Signature.Length;
            h5.VendorBootconfigSize = (uint)Bootconfig.Length;
            DataHelper.Serialize(stream, h5);
            WritePadded(stream, Kernel, 4096);
            WritePadded(stream, Ramdisk, 4096);
            WritePadded(stream, Signature, 4096);
            WritePadded(stream, Bootconfig, 4096);
        }
        else if (Header is BootImageHeaderV6 h6)
        {
            h6.KernelSize = (uint)Kernel.Length;
            h6.RamdiskSize = (uint)Ramdisk.Length;
            h6.SignatureSize = (uint)Signature.Length;
            h6.VendorBootconfigSize = (uint)Bootconfig.Length;
            DataHelper.Serialize(stream, h6);
            WritePadded(stream, Kernel, 4096);
            WritePadded(stream, Ramdisk, 4096);
            WritePadded(stream, Signature, 4096);
            WritePadded(stream, Bootconfig, 4096);
        }
        else if (Header is VendorBootImageHeaderV3 v3)
        {
            v3.VendorRamdiskSize = (uint)Ramdisk.Length;
            v3.DtbSize = (uint)Dtb.Length;
            DataHelper.Serialize(stream, v3);
            WritePadded(stream, Ramdisk, v3.PageSize);
            WritePadded(stream, Dtb, v3.PageSize);
        }
        else if (Header is VendorBootImageHeaderV4 v4)
        {
            v4.VendorRamdiskSize = (uint)Ramdisk.Length;
            v4.DtbSize = (uint)Dtb.Length;
            v4.VendorRamdiskTableSize = (uint)VendorRamdiskTable.Length;
            v4.BootconfigSize = (uint)Bootconfig.Length;
            DataHelper.Serialize(stream, v4);
            WritePadded(stream, Ramdisk, v4.PageSize);
            WritePadded(stream, Dtb, v4.PageSize);
            WritePadded(stream, VendorRamdiskTable, v4.PageSize);
            WritePadded(stream, Bootconfig, v4.PageSize);
        }
        else
        {
            throw new NotSupportedException("Unknown header type: " + Header.GetType().Name);
        }
    }

    private static void WritePadded(Stream stream, byte[] data, uint pageSize)
    {
        if (data.Length == 0) return;
        stream.Write(data, 0, data.Length);
        long padding = (pageSize - (data.Length % pageSize)) % pageSize;
        if (padding > 0)
        {
            byte[] padData = new byte[padding];
            stream.Write(padData, 0, (int)padding);
        }
    }

    private static byte[] ReadPadded(Stream stream, long offset, uint size, uint pageSize)
    {
        if (size == 0) return [];
        byte[] data = new byte[size];
        stream.Seek(offset, SeekOrigin.Begin);
        ReadStreamFully(stream, data, (int)size);
        return data;
    }

    private static void ReadStreamFully(Stream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }


}

