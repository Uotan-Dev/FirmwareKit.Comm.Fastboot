using FirmwareKit.Comm.Fastboot.Utils;
using System.Runtime.InteropServices;
using System.Text;

namespace FirmwareKit.Comm.Fastboot.DataModel;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV0
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    public uint KernelSize;
    public uint KernelAddr;

    public uint RamdiskSize;
    public uint RamdiskAddr;

    public uint SecondSize;
    public uint SecondAddr;

    public uint TagsAddr;
    public uint PageSize;

    public uint HeaderVersion;

    public uint OsVersion;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Name;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
    public byte[] Cmdline;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public uint[] Id;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
    public byte[] ExtraCmdline;

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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV1
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    public uint KernelSize;
    public uint KernelAddr;

    public uint RamdiskSize;
    public uint RamdiskAddr;

    public uint SecondSize;
    public uint SecondAddr;

    public uint TagsAddr;
    public uint PageSize;

    public uint HeaderVersion;

    public uint OsVersion;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Name;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
    public byte[] Cmdline;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public uint[] Id;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
    public byte[] ExtraCmdline;

    public uint HeaderSize;

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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV2
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    public uint KernelSize;
    public uint KernelAddr;

    public uint RamdiskSize;
    public uint RamdiskAddr;

    public uint SecondSize;
    public uint SecondAddr;

    public uint TagsAddr;
    public uint PageSize;

    public uint HeaderVersion;

    public uint OsVersion;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Name;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
    public byte[] Cmdline;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public uint[] Id;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
    public byte[] ExtraCmdline;

    public uint HeaderSize;

    public uint DtbSize;
    public ulong DtbAddr;

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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV3
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    public uint KernelSize;
    public uint RamdiskSize;
    public uint OsVersion;
    public uint HeaderSize;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public uint[] Reserved;

    public uint HeaderVersion;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1536)]
    public byte[] Cmdline;

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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV4
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    public uint KernelSize;
    public uint RamdiskSize;
    public uint OsVersion;
    public uint HeaderSize;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public uint[] Reserved;

    public uint HeaderVersion;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1536)]
    public byte[] Cmdline;

    public uint SignatureSize;

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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV5
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    public uint KernelSize;
    public uint RamdiskSize;
    public uint OsVersion;
    public uint HeaderSize;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public uint[] Reserved;

    public uint HeaderVersion;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1536)]
    public byte[] Cmdline;

    public uint SignatureSize;
    public uint VendorBootconfigSize;

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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootImageHeaderV6
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    public uint KernelSize;
    public uint RamdiskSize;
    public uint OsVersion;
    public uint HeaderSize;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public uint[] Reserved;

    public uint HeaderVersion;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1536)]
    public byte[] Cmdline;

    public uint SignatureSize;
    public uint VendorBootconfigSize;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Reserved1;

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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VendorBootImageHeaderV3
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    public uint HeaderVersion;
    public uint PageSize;
    public uint KernelAddr;
    public uint RamdiskAddr;
    public uint VendorRamdiskSize;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)]
    public byte[] Cmdline;

    public uint TagsAddr;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Name;

    public uint HeaderSize;
    public uint DtbSize;
    public ulong DtbAddr;

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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VendorBootImageHeaderV4
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Magic;

    public uint HeaderVersion;
    public uint PageSize;
    public uint KernelAddr;
    public uint RamdiskAddr;
    public uint VendorRamdiskSize;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)]
    public byte[] Cmdline;

    public uint TagsAddr;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Name;

    public uint HeaderSize;
    public uint DtbSize;
    public ulong DtbAddr;

    public uint VendorRamdiskTableSize;
    public uint VendorRamdiskTableEntryNum;
    public uint VendorRamdiskTableEntrySize;
    public uint BootconfigSize;

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

public class BootImage
{
    public object Header { get; set; }
    public byte[] Kernel { get; set; } = [];
    public byte[] Ramdisk { get; set; } = [];
    public byte[] Second { get; set; } = [];
    public byte[] Dtb { get; set; } = [];
    public byte[] Signature { get; set; } = [];
    public byte[] VendorRamdiskTable { get; set; } = [];
    public byte[] Bootconfig { get; set; } = [];

    public BootImage(object header)
    {
        Header = header;
    }

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
        long offset = header.HeaderSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, pageSize);
        offset += (header.KernelSize + pageSize - 1) / pageSize * pageSize;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, pageSize);
        offset += (header.RamdiskSize + pageSize - 1) / pageSize * pageSize;
        Second = ReadPadded(stream, offset, header.SecondSize, pageSize);
    }

    private void ReadData(Stream stream, BootImageHeaderV2 header)
    {
        uint pageSize = header.PageSize;
        long offset = header.HeaderSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, pageSize);
        offset += (header.KernelSize + pageSize - 1) / pageSize * pageSize;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, pageSize);
        offset += (header.RamdiskSize + pageSize - 1) / pageSize * pageSize;
        Second = ReadPadded(stream, offset, header.SecondSize, pageSize);
    }

    private void ReadData(Stream stream, BootImageHeaderV3 header)
    {
        long offset = header.HeaderSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, 4096);
        offset += (header.KernelSize + 4095) / 4096 * 4096;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, 4096);
    }

    private void ReadData(Stream stream, BootImageHeaderV4 header)
    {
        long offset = header.HeaderSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, 4096);
        offset += (header.KernelSize + 4095) / 4096 * 4096;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, 4096);
        offset += (header.RamdiskSize + 4095) / 4096 * 4096;
        Signature = ReadPadded(stream, offset, header.SignatureSize, 4096);
    }

    private void ReadData(Stream stream, BootImageHeaderV5 header)
    {
        long offset = header.HeaderSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, 4096);
        offset += (header.KernelSize + 4095) / 4096 * 4096;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, 4096);
        offset += (header.RamdiskSize + 4095) / 4096 * 4096;
        Signature = ReadPadded(stream, offset, header.SignatureSize, 4096);
        offset += (header.SignatureSize + 4095) / 4096 * 4096;
        Bootconfig = ReadPadded(stream, offset, header.VendorBootconfigSize, 4096);
    }

    private void ReadData(Stream stream, BootImageHeaderV6 header)
    {
        long offset = header.HeaderSize;
        Kernel = ReadPadded(stream, offset, header.KernelSize, 4096);
        offset += (header.KernelSize + 4095) / 4096 * 4096;
        Ramdisk = ReadPadded(stream, offset, header.RamdiskSize, 4096);
        offset += (header.RamdiskSize + 4095) / 4096 * 4096;
        Signature = ReadPadded(stream, offset, header.SignatureSize, 4096);
        offset += (header.SignatureSize + 4095) / 4096 * 4096;
        Bootconfig = ReadPadded(stream, offset, header.VendorBootconfigSize, 4096);
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

    public string GetBootconfigText()
    {
        if (Bootconfig == null || Bootconfig.Length == 0) return "";
        return Encoding.ASCII.GetString(Bootconfig).TrimEnd('\0');
    }

    public void SetBootconfigText(string text)
    {
        Bootconfig = Encoding.ASCII.GetBytes(text + "\0");
    }

    public void AddBootconfig(string key, string value)
    {
        string current = GetBootconfigText();
        SetBootconfigText(current + (string.IsNullOrEmpty(current) ? "" : "\n") + $"{key} = \"{value}\"");
    }

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

