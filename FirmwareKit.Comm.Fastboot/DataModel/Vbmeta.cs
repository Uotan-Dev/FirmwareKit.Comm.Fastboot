
using System.Runtime.InteropServices;
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// Android Verified Boot (AVB) vbmeta header structure.
/// <para>Android 验证启动 (AVB) vbmeta 头结构。</para>
/// </summary>
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

    /// <summary>
    /// Checks whether the vbmeta header contains a valid AVB0 magic number.
    /// <para>检查 vbmeta 头是否包含有效的 AVB0 魔数。</para>
    /// </summary>
    public bool IsValid() => Encoding.ASCII.GetString(Magic) == "AVB0";

    /// <summary>
    /// Deserializes a VbmetaHeader from a byte array.
    /// <para>从字节数组反序列化 VbmetaHeader。</para>
    /// </summary>
    public static VbmetaHeader FromBytes(byte[] data)
    {
        return DataHelper.Bytes2Struct<VbmetaHeader>(data, Marshal.SizeOf<VbmetaHeader>());
    }
}

/// <summary>
/// Android Verified Boot (AVB) footer structure, typically found at the end of a partition.
/// <para>Android 验证启动 (AVB) 页脚结构，通常位于分区末尾。</para>
/// </summary>
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

    /// <summary>
    /// Checks whether the AVB footer contains a valid AVBf magic number.
    /// <para>检查 AVB 页脚是否包含有效的 AVBf 魔数。</para>
    /// </summary>
    public bool IsValid() => Encoding.ASCII.GetString(Magic) == "AVBf";

    /// <summary>
    /// Deserializes an AvbFooter from a byte array (minimum 64 bytes).
    /// <para>从字节数组反序列化 AvbFooter（最少 64 字节）。</para>
    /// </summary>
    public static AvbFooter FromBytes(byte[] data)
    {
        if (data.Length < 64) throw new ArgumentException("Data too small for AvbFooter.");
        return DataHelper.Bytes2Struct<AvbFooter>(data, 64);
    }
}

/// <summary>
/// AVB algorithm types for signing vbmeta images.
/// <para>用于签名 vbmeta 镜像的 AVB 算法类型。</para>
/// </summary>
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

/// <summary>
/// Flags that control vbmeta image verification behavior (disable hashtree, disable verification).
/// <para>控制 vbmeta 镜像验证行为的标志（禁用哈希树、禁用验证）。</para>
/// </summary>
[Flags]
public enum VbmetaImageFlags : uint
{
    None = 0,
    HashtreeDisabled = 1 << 0,
    VerificationDisabled = 1 << 1,
}

