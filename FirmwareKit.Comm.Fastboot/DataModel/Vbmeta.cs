using FirmwareKit.AVB.Enums;

namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// Defines flags for vbmeta image behavior, such as disabling hashtree or verification.
/// <para>定义 vbmeta 镜像行为的标志，例如禁用 hashtree 或验证。</para>
/// </summary>
[Flags]
public enum VbmetaImageFlags : uint
{
    /// <summary>
    /// No special flags set.
    /// <para>未设置特殊标志。</para>
    /// </summary>
    None = 0,

    /// <summary>
    /// Hashtree verification is disabled for this partition.
    /// <para>此分区已禁用 hashtree 验证。</para>
    /// </summary>
    HashtreeDisabled = 1 << 0,

    /// <summary>
    /// Cryptographic verification is disabled for this partition.
    /// <para>此分区已禁用加密验证。</para>
    /// </summary>
    VerificationDisabled = 1 << 1,
}

/// <summary>
/// Utility class for converting between FirmwareKit AVB flags and vbmeta image flags.
/// <para>用于在 FirmwareKit AVB 标志和 vbmeta 镜像标志之间转换的工具类。</para>
/// </summary>
public static class VbmetaUtils
{
    /// <summary>
    /// Converts VbmetaImageFlags to AVB VBMetaImageFlags.
    /// <para>将 VbmetaImageFlags 转换为 AVB VBMetaImageFlags。</para>
    /// </summary>
    /// <param name="flags">The vbmeta image flags to convert. <para>要转换的 vbmeta 镜像标志。</para></param>
    /// <returns>The corresponding AVB VBMetaImageFlags value. <para>对应的 AVB VBMetaImageFlags 值。</para></returns>
    public static AvbVBMetaImageFlags ToAvbVBMetaImageFlags(VbmetaImageFlags flags)
    {
        var result = AvbVBMetaImageFlags.None;
        if ((flags & VbmetaImageFlags.HashtreeDisabled) != 0)
            result |= AvbVBMetaImageFlags.HashtreeDisabled;
        if ((flags & VbmetaImageFlags.VerificationDisabled) != 0)
            result |= AvbVBMetaImageFlags.VerificationDisabled;
        return result;
    }

    /// <summary>
    /// Converts AVB VBMetaImageFlags to VbmetaImageFlags.
    /// <para>将 AVB VBMetaImageFlags 转换为 VbmetaImageFlags。</para>
    /// </summary>
    /// <param name="flags">The AVB flags to convert. <para>要转换的 AVB 标志。</para></param>
    /// <returns>The corresponding VbmetaImageFlags value. <para>对应的 VbmetaImageFlags 值。</para></returns>
    public static VbmetaImageFlags FromAvbVBMetaImageFlags(AvbVBMetaImageFlags flags)
    {
        var result = VbmetaImageFlags.None;
        if ((flags & AvbVBMetaImageFlags.HashtreeDisabled) != 0)
            result |= VbmetaImageFlags.HashtreeDisabled;
        if ((flags & AvbVBMetaImageFlags.VerificationDisabled) != 0)
            result |= VbmetaImageFlags.VerificationDisabled;
        return result;
    }
}