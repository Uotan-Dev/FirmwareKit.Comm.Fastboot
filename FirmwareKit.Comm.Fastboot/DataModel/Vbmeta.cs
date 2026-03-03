using System.Runtime.InteropServices;
using System.Text;

namespace FirmwareKit.Comm.Fastboot.DataModel;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VbmetaHeader
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] Magic;

    public uint RequiredLibavbVersionMajor;
    public uint RequiredLibavbVersionMinor;
    public uint AuthenticationDataBlockSize;
    public uint AuxiliaryDataBlockSize;
    public uint AlgorithmType;
    public ulong HashOffset;
    public ulong HashSize;
    public ulong SignatureOffset;
    public ulong SignatureSize;
    public ulong PublicKeyValueOffset;
    public ulong PublicKeyValueSize;
    public ulong PublicKeyMetadataOffset;
    public ulong PublicKeyMetadataSize;
    public ulong DescriptorsOffset;
    public ulong DescriptorsSize;
    public ulong RollbackIndex;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] Reserved0;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 47)]
    public byte[] ReleaseString;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
    public byte[] Reserved;

    public bool IsValid() => Encoding.ASCII.GetString(Magic) == "AVB0";

    public static VbmetaHeader FromBytes(byte[] data)
    {
        return DataHelper.Bytes2Struct<VbmetaHeader>(data, Marshal.SizeOf<VbmetaHeader>());
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AvbFooter
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] Magic;

    public uint VersionMajor;
    public uint VersionMinor;
    public ulong OriginalImageSize;
    public ulong VbmetaOffset;
    public ulong VbmetaSize;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
    public byte[] Reserved;

    public bool IsValid() => Encoding.ASCII.GetString(Magic) == "AVBf";

    public static AvbFooter FromBytes(byte[] data)
    {
        if (data.Length < 64) throw new ArgumentException("Data too small for AvbFooter.");
        return DataHelper.Bytes2Struct<AvbFooter>(data, 64);
    }
}

public enum AvbAlgorithmType : uint
{
    NONE = 0,
    SHA256_RSA2048 = 1,
    SHA256_RSA4096 = 2,
    SHA256_RSA8192 = 3,
    SHA512_RSA2048 = 4,
    SHA512_RSA4096 = 5,
    SHA512_RSA8192 = 6
}

public static class VbmetaFlags
{
    public const uint AVB_VBMETA_IMAGE_FLAGS_HASHTREE_DISABLED = (1 << 0);
    public const uint AVB_VBMETA_IMAGE_FLAGS_VERIFICATION_DISABLED = (1 << 1);


}