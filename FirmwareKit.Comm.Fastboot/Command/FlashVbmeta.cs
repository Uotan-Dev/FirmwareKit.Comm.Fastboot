using System;
using System.IO;
using System.Runtime.InteropServices;
using FirmwareKit.Comm.Fastboot.DataModel;
using FirmwareKit.Lp;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Flashes VBMeta image and optionally disables verification (corresponding to --disable-verity / --disable-verification)
        /// </summary>
        public FastbootResponse FlashVbmeta(string partition, string filePath, bool disableVerity = false, bool disableVerification = false)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);
            byte[] data = File.ReadAllBytes(filePath);

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
                    if (disableVerity) header.Flags |= VbmetaFlags.AVB_VBMETA_IMAGE_FLAGS_HASHTREE_DISABLED;
                    if (disableVerification) header.Flags |= VbmetaFlags.AVB_VBMETA_IMAGE_FLAGS_VERIFICATION_DISABLED;

                    byte[] headerBytes = DataHelper.Struct2Bytes(header);
                    Array.Copy(headerBytes, 0, data, 0, headerBytes.Length);
                    NotifyCurrentStep($"Modified VBMeta flags: verity={disableVerity}, verification={disableVerification}");
                }
            }

            return FlashUnsparseImage(partition, new MemoryStream(data), data.Length);
        }
    }
}
