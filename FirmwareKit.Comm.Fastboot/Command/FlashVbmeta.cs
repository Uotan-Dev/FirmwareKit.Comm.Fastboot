using FirmwareKit.AVB.Core;
using FirmwareKit.AVB.Enums;
using FirmwareKit.AVB.Security;
using FirmwareKit.AVB.VBMeta;
using System.Security.Cryptography;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    private const int AvbVBMetaImageFlagsOffset = 123;
    private const int AvbVBMetaImageFlagsSize = 4;

    public FastbootResponse FlashVbmeta(string partition, string filePath, bool disableVerity = false, bool disableVerification = false, string? privateKeyPath = null)
    {
        if (string.IsNullOrWhiteSpace(partition)) throw new ArgumentException("Partition name is required.", nameof(partition));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required.", nameof(filePath));
        if (!File.Exists(filePath)) throw new FileNotFoundException($"Vbmeta image file not found: {filePath}", filePath);

        byte[] data;
        try
        {
            data = File.ReadAllBytes(filePath);
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new IOException($"Failed to read vbmeta image file: {filePath}", ex);
        }

        byte[]? privateKeyData = null;
        if (!string.IsNullOrWhiteSpace(privateKeyPath))
        {
            if (!File.Exists(privateKeyPath))
                throw new FileNotFoundException("Private key file not found.", privateKeyPath);
            privateKeyData = File.ReadAllBytes(privateKeyPath);
        }

        return FlashVbmeta(partition, data, disableVerity, disableVerification, privateKeyData);
    }

    public FastbootResponse FlashVbmeta(string partition, byte[] data, bool disableVerity = false, bool disableVerification = false, byte[]? privateKeyData = null)
    {
        if (string.IsNullOrWhiteSpace(partition)) throw new ArgumentException("Partition name is required.", nameof(partition));
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("Vbmeta data is empty.", nameof(data));

        bool hasValidFooter = false;
        AvbFooter footer = default;

        if (data.Length >= 64)
        {
            try
            {
                byte[] footerBytes = new byte[64];
                Array.Copy(data, data.Length - 64, footerBytes, 0, 64);
                footer = AvbFooter.FromBytes(footerBytes);
                if (footer.IsValid)
                {
                    hasValidFooter = true;
                    NotifyCurrentStep($"AVB Footer detected (Vbmeta origin size: {footer.OriginalImageSize}, Vbmeta size: {footer.VBMetaSize})");
                    FastbootDebug.Log($"AVB Footer: originSize={footer.OriginalImageSize}, vbmetaOffset={footer.VBMetaOffset}, vbmetaSize={footer.VBMetaSize}");
                }
            }
            catch (Exception ex)
            {
                FastbootDebug.Log($"AVB Footer parse warning: {ex.Message}");
            }
        }

        if (disableVerity || disableVerification)
        {
            try
            {
                var modificationResult = ModifyVbmetaFlags(data, disableVerity, disableVerification, hasValidFooter, hasValidFooter ? footer : default);
                data = modificationResult.Data;

                if (modificationResult.FlagsWereModified)
                {
                    if (privateKeyData != null)
                    {
                        try
                        {
                            data = ReSignVbmetaImage(data, privateKeyData);
                            NotifyCurrentStep("VBMeta image has been re-signed with the provided private key.");
                            FastbootDebug.Log("VBMeta image successfully re-signed");
                        }
                        catch (Exception ex)
                        {
                            FastbootDebug.Log($"Failed to re-sign vbmeta image: {ex.Message}");
                            throw new InvalidOperationException("Failed to re-sign vbmeta image with the provided private key.", ex);
                        }
                    }
                    else
                    {
                        NotifyCurrentStep("WARNING: VBMeta flags have been modified. The image signature is now INVALID and must be re-signed before use with devices that have secure boot enabled.");
                        FastbootDebug.Log("VBMeta flags were modified - signature invalidation warning logged");
                    }
                }
            }
            catch (Exception ex)
            {
                FastbootDebug.Log($"Failed to modify vbmeta flags: {ex.Message}");
                throw new InvalidOperationException("Failed to modify vbmeta flags. The image may be corrupted or in an unsupported format.", ex);
            }
        }

        using var ms = new MemoryStream(data);
        return FlashUnsparseImage(partition, ms, data.Length);
    }

    private class VbmetaFlagsModificationResult
    {
        public byte[] Data { get; set; } = [];
        public bool FlagsWereModified { get; set; }
        public bool SignatureInvalidated { get; set; }
    }

    private VbmetaFlagsModificationResult ModifyVbmetaFlags(byte[] data, bool disableVerity, bool disableVerification, bool hasValidFooter, AvbFooter footer)
    {
        var result = new VbmetaFlagsModificationResult { Data = data };

        var vbmetaImage = new AvbVBMetaImage(data);
        var verificationResult = vbmetaImage.VerifyIntegrity();

        if (verificationResult != AvbVBMetaVerifyResult.Ok)
        {
            var resultStr = AvbVBMetaImage.ResultToString(verificationResult);
            throw new InvalidOperationException(
                $"Vbmeta integrity verification failed before modification: {resultStr}. " +
                "The vbmeta image may be corrupted. Aborting flags modification to prevent potential device boot failure.");
        }

        var currentFlags = vbmetaImage.Header.Flags;
        var targetFlags = currentFlags;

        if (disableVerity)
            targetFlags |= (uint)AvbVBMetaImageFlags.HashtreeDisabled;
        if (disableVerification)
            targetFlags |= (uint)AvbVBMetaImageFlags.VerificationDisabled;

        if (targetFlags == currentFlags)
        {
            result.Data = data;
            result.FlagsWereModified = false;
            result.SignatureInvalidated = false;
            FastbootDebug.Log($"VBMeta flags unchanged (current: {currentFlags}, target: {targetFlags})");
            return result;
        }

        if (!ValidateVbmetaImageSize(data, hasValidFooter, hasValidFooter ? footer : default))
        {
            throw new InvalidOperationException(
                $"Vbmeta image size ({data.Length} bytes) is too small to contain a valid AVB header. " +
                "Minimum expected size is 256 bytes.");
        }

        data = (byte[])data.Clone();

        WriteFlagsToImage(data, targetFlags);

        result.Data = data;
        result.FlagsWereModified = true;
        result.SignatureInvalidated = true;

        bool hashTreeDisabled = (targetFlags & (uint)AvbVBMetaImageFlags.HashtreeDisabled) != 0;
        bool verificationDisabled = (targetFlags & (uint)AvbVBMetaImageFlags.VerificationDisabled) != 0;

        NotifyCurrentStep($"Modified VBMeta flags: HashtreeDisabled={hashTreeDisabled}, VerificationDisabled={verificationDisabled}");
        FastbootDebug.Log($"VBMeta flags changed from {currentFlags} to {targetFlags} (bytes at offset {AvbVBMetaImageFlagsOffset})");
        FastbootDebug.Log("WARNING: Image signature has been invalidated by flags modification!");

        return result;
    }

    private static bool ValidateVbmetaImageSize(byte[] data, bool hasValidFooter, AvbFooter footer)
    {
        int minimumSize = AvbVBMetaImageFlagsOffset + AvbVBMetaImageFlagsSize;

        if (hasValidFooter)
        {
            if (footer.VBMetaOffset < (ulong)minimumSize)
            {
                FastbootDebug.Log($"Warning: Footer VBMetaOffset ({footer.VBMetaOffset}) is less than expected minimum ({minimumSize})");
            }
            return true;
        }

        return data.Length >= minimumSize;
    }

    private static void WriteFlagsToImage(byte[] data, uint flags)
    {
        if (AvbVBMetaImageFlagsOffset + AvbVBMetaImageFlagsSize > data.Length)
        {
            throw new InvalidOperationException(
                $"Cannot write flags at offset {AvbVBMetaImageFlagsOffset}: " +
                $"image size ({data.Length}) is too small. Minimum required: {AvbVBMetaImageFlagsOffset + AvbVBMetaImageFlagsSize}");
        }

        data[AvbVBMetaImageFlagsOffset] = (byte)(flags & 0xFF);
        data[AvbVBMetaImageFlagsOffset + 1] = (byte)((flags >> 8) & 0xFF);
        data[AvbVBMetaImageFlagsOffset + 2] = (byte)((flags >> 16) & 0xFF);
        data[AvbVBMetaImageFlagsOffset + 3] = (byte)((flags >> 24) & 0xFF);
    }

    private const ulong AvbVbmetaHeaderSize = 256UL;

#if NET5_0_OR_GREATER
    public byte[] ReSignVbmetaImage(byte[] data, byte[] privateKeyPkcs8, string algorithmName = "SHA256withRSA")
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (privateKeyPkcs8 == null) throw new ArgumentNullException(nameof(privateKeyPkcs8));
        if (data.Length == 0) throw new ArgumentException("Vbmeta data is empty.", nameof(data));

        var vbmetaImage = new AvbVBMetaImage(data);
        var verificationResult = vbmetaImage.VerifyIntegrity();

        if (verificationResult != AvbVBMetaVerifyResult.Ok)
        {
            FastbootDebug.Log($"Warning: Re-signing image with failed verification status: {AvbVBMetaImage.ResultToString(verificationResult)}");
        }

        if (!ValidateVbmetaImageSize(data, false, default))
        {
            throw new InvalidOperationException("Vbmeta image is too small to contain a valid AVB header.");
        }

        var headerSize = AvbVbmetaHeaderSize;
        if (headerSize > (ulong)data.Length)
        {
            throw new InvalidOperationException($"Invalid header size: {headerSize}");
        }

        var signatureOffset = vbmetaImage.Header.SignatureOffset;
        var signatureSize = vbmetaImage.Header.SignatureSize;
        var hashOffset = vbmetaImage.Header.HashOffset;
        var hashSize = vbmetaImage.Header.HashSize;

        if (signatureOffset < headerSize || signatureOffset >= (ulong)data.Length ||
            signatureSize == 0 || signatureOffset + signatureSize > (ulong)data.Length ||
            hashOffset < headerSize || hashOffset >= (ulong)data.Length ||
            hashSize == 0 || hashOffset + hashSize > (ulong)data.Length)
        {
            throw new InvalidOperationException("Invalid signature or hash offset/size in vbmeta header.");
        }

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);

        var dataToSign = data.AsSpan((int)hashOffset, (int)hashSize).ToArray();

        var signature = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        data = (byte[])data.Clone();
        Array.Copy(signature, 0, data, (int)signatureOffset, Math.Min(signature.Length, (int)signatureSize));

        FastbootDebug.Log($"Re-signed vbmeta image with {algorithmName}. Signature size: {signature.Length} bytes");

        var verifyResult = new AvbVBMetaImage(data).VerifyIntegrity();
        if (verifyResult != AvbVBMetaVerifyResult.Ok)
        {
            FastbootDebug.Log($"Warning: Re-signed image verification still fails: {AvbVBMetaImage.ResultToString(verifyResult)}");
        }

        return data;
    }
#else
    public byte[] ReSignVbmetaImage(byte[] data, byte[] privateKeyPkcs8, string algorithmName = "SHA256withRSA")
    {
        throw new NotSupportedException("Re-signing vbmeta images requires .NET 5.0 or later. Please use a newer .NET runtime.");
    }
#endif
}
