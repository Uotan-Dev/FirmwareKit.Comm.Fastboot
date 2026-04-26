using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Flashes a vbmeta image file to the specified partition with optional verity/verification flags.
    /// <para>将 vbmeta 镜像文件刷写到指定分区，可选择禁用 verity/verification 标志。</para>
    /// </summary>
    /// <param name="partition">The partition to flash vbmeta to. <para>要刷写 vbmeta 的分区。</para></param>
    /// <param name="filePath">Path to the vbmeta image file. <para>vbmeta 镜像文件的路径。</para></param>
    /// <param name="disableVerity">Whether to disable dm-verity (hashtree). <para>是否禁用 dm-verity（哈希树）。</para></param>
    /// <param name="disableVerification">Whether to disable verification. <para>是否禁用验证。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse FlashVbmeta(string partition, string filePath, bool disableVerity = false, bool disableVerification = false)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);
        byte[] data = File.ReadAllBytes(filePath);
        return FlashVbmeta(partition, data, disableVerity, disableVerification);
    }

    /// <summary>
    /// Flashes vbmeta data bytes to the specified partition with optional verity/verification flags.
    /// <para>将 vbmeta 数据字节刷写到指定分区，可选择禁用 verity/verification 标志。</para>
    /// </summary>
    /// <param name="partition">The partition to flash vbmeta to. <para>要刷写 vbmeta 的分区。</para></param>
    /// <param name="data">The vbmeta image data bytes. <para>vbmeta 镜像数据字节。</para></param>
    /// <param name="disableVerity">Whether to disable dm-verity (hashtree). <para>是否禁用 dm-verity（哈希树）。</para></param>
    /// <param name="disableVerification">Whether to disable verification. <para>是否禁用验证。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse FlashVbmeta(string partition, byte[] data, bool disableVerity = false, bool disableVerification = false)
    {
        if (data.Length < Marshal.SizeOf<VbmetaHeader>())
            throw new Exception("vbmeta image too small");

        if (data.Length >= 64)
        {
            byte[] footerBytes = new byte[64];
            Array.Copy(data, data.Length - 64, footerBytes, 0, 64);
            try
            {
                var footer = AvbFooter.FromBytes(footerBytes);
                if (footer.IsValid())
                {
                    NotifyCurrentStep($"AVB Footer detected (Vbmeta origin size: {footer.OriginalImageSize}, Vbmeta size: {footer.VbmetaSize})");
                }
            }
            catch { }
        }

        if (disableVerity || disableVerification)
        {
            var header = VbmetaHeader.FromBytes(data);
            if (header.Magic[0] == (byte)'A' && header.Magic[1] == (byte)'V' && header.Magic[2] == (byte)'B' && header.Magic[3] == (byte)'0')
            {
                if (disableVerity) header.Flags |= (uint)VbmetaImageFlags.HashtreeDisabled;
                if (disableVerification) header.Flags |= (uint)VbmetaImageFlags.VerificationDisabled;

                byte[] headerBytes = DataHelper.Struct2Bytes(header);
                Array.Copy(headerBytes, 0, data, 0, headerBytes.Length);
                NotifyCurrentStep($"Modified VBMeta flags: verity={disableVerity}, verification={disableVerification}");
            }
        }

        return FlashUnsparseImage(partition, new MemoryStream(data), data.Length);
    }
}
