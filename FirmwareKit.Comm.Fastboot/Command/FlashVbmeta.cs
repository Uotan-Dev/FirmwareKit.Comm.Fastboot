using FirmwareKit.AVB.Core;
using FirmwareKit.AVB.Enums;
using FirmwareKit.AVB.Security;
using FirmwareKit.AVB.VBMeta;
using System.Security.Cryptography;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    // VBMeta flags live at offset 120 of the 256-byte AVB header and are stored big-endian
    // (network byte order), per the AVB spec and FirmwareKit.AVB's AvbVBMetaImageHeader.ToBytes.
    // We delegate to the AVB library instead of hand-writing bytes, so offset and endianness
    // cannot drift from the spec.

    /// <summary>
    /// Flashes a vbmeta image file to the specified partition, optionally disabling verity/verification and re-signing.
    /// <para>将 vbmeta 镜像文件刷写到指定分区，可选禁用 verity/verification 并重新签名。</para>
    /// </summary>
    /// <param name="partition">Target partition to flash (e.g., "vbmeta"). <para>目标刷写分区（如 "vbmeta"）。</para></param>
    /// <param name="filePath">Path to the vbmeta image file. <para>vbmeta 镜像文件路径。</para></param>
    /// <param name="disableVerity">Whether to clear the disable-verity flag. <para>是否清除 disable-verity 标志。</para></param>
    /// <param name="disableVerification">Whether to clear the disable-verification flag. <para>是否清除 disable-verification 标志。</para></param>
    /// <param name="privateKeyPath">Optional PKCS#8 private key to re-sign the image. <para>用于重新签名的可选 PKCS#8 私钥。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
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

    /// <summary>
    /// Flashes a vbmeta image byte array to the specified partition, optionally disabling verity/verification and re-signing.
    /// <para>将 vbmeta 镜像字节数组刷写到指定分区，可选禁用 verity/verification 并重新签名。</para>
    /// </summary>
    /// <param name="partition">Target partition to flash (e.g., "vbmeta"). <para>目标刷写分区（如 "vbmeta"）。</para></param>
    /// <param name="data">Raw vbmeta image bytes. <para>原始 vbmeta 镜像字节。</para></param>
    /// <param name="disableVerity">Whether to clear the disable-verity flag. <para>是否清除 disable-verity 标志。</para></param>
    /// <param name="disableVerification">Whether to clear the disable-verification flag. <para>是否清除 disable-verification 标志。</para></param>
    /// <param name="privateKeyData">Optional PKCS#8 private key to re-sign the image. <para>用于重新签名的可选 PKCS#8 私钥。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
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

        // Locate the vbmeta block. For footered partition images the header is not at offset 0;
        // the AVB library's AvbVBMetaImage currently parses from offset 0, so verify integrity on
        // the vbmeta block itself (which is what the bootloader verifies).
        var (blockOffset, blockLength, _, _) = LocateVbmetaBlock(data);
        byte[] block = new byte[blockLength];
        Array.Copy(data, blockOffset, block, 0, blockLength);

        var vbmetaImage = new AvbVBMetaImage(block);
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

        if (blockLength < AvbVBMetaImageHeader.Size)
        {
            throw new InvalidOperationException(
                $"Vbmeta block size ({blockLength} bytes) is too small to contain a valid AVB header. " +
                $"Minimum expected size is {AvbVBMetaImageHeader.Size} bytes.");
        }

        data = (byte[])data.Clone();

        // Write flags into the header within the vbmeta block at the correct offset/endianness.
        WriteFlagsToImage(data, blockOffset, targetFlags);

        result.Data = data;
        result.FlagsWereModified = true;
        result.SignatureInvalidated = true;

        bool hashTreeDisabled = (targetFlags & (uint)AvbVBMetaImageFlags.HashtreeDisabled) != 0;
        bool verificationDisabled = (targetFlags & (uint)AvbVBMetaImageFlags.VerificationDisabled) != 0;

        NotifyCurrentStep($"Modified VBMeta flags: HashtreeDisabled={hashTreeDisabled}, VerificationDisabled={verificationDisabled}");
        FastbootDebug.Log($"VBMeta flags changed from {currentFlags} to {targetFlags} via AvbVBMetaImageHeader.ToBytes (offset 120, big-endian)");
        FastbootDebug.Log("WARNING: Image signature has been invalidated by flags modification!");

        return result;
    }

    private static bool ValidateVbmetaImageSize(byte[] data, bool hasValidFooter, AvbFooter footer)
    {
        // A valid vbmeta image must contain at least the 256-byte AVB header. When a footer is
        // present, the VBMeta block it points to must itself be at least that large.
        int minimumSize = AvbVBMetaImageHeader.Size;

        if (hasValidFooter)
        {
            if (footer.VBMetaSize > 0 && footer.VBMetaSize < (ulong)minimumSize)
            {
                FastbootDebug.Log($"Warning: Footer VBMetaSize ({footer.VBMetaSize}) is less than expected minimum ({minimumSize})");
            }
            return true;
        }

        return data.Length >= minimumSize;
    }

    // Writes the flags field via FirmwareKit.AVB's header (de)serializer, which encodes it at the
    // correct offset (120) in big-endian per the AVB wire format. blockOffset is the start of the
    // vbmeta block within `data` (0 for standalone vbmeta, footer.VBMetaOffset for footered images).
    private static void WriteFlagsToImage(byte[] data, int blockOffset, uint flags)
    {
        if (blockOffset < 0 || blockOffset + AvbVBMetaImageHeader.Size > data.Length)
        {
            throw new InvalidOperationException(
                $"Cannot write flags: vbmeta block at offset {blockOffset} is out of range for image size {data.Length}.");
        }

        var header = AvbVBMetaImageHeader.FromBytes(data.AsSpan(blockOffset, AvbVBMetaImageHeader.Size));
        header = header with { Flags = flags };
        header.ToBytes(data.AsSpan(blockOffset, AvbVBMetaImageHeader.Size));
    }

    // Locates the vbmeta block within a (possibly footered) partition image. For standalone vbmeta
    // the block is the whole image. For images with an AVB footer, the header/hash/signature live in
    // the block described by the footer, not at offset 0.
    private static (int Offset, int Length, AvbFooter Footer, bool HasFooter) LocateVbmetaBlock(byte[] data)
    {
        if (data.Length >= AvbFooter.Size)
        {
            Span<byte> footerBytes = stackalloc byte[AvbFooter.Size];
            data.AsSpan(data.Length - AvbFooter.Size).CopyTo(footerBytes);
            if (AvbFooter.TryFromBytes(footerBytes, out var footer) && footer.IsValid)
            {
                int off = checked((int)footer.VBMetaOffset);
                int len = checked((int)footer.VBMetaSize);
                if (len >= AvbVBMetaImageHeader.Size &&
                    off >= 0 &&
                    off + len <= data.Length - AvbFooter.Size)
                {
                    return (off, len, footer, true);
                }
                FastbootDebug.Log($"AVB footer present but vbmeta block (offset={off}, size={len}) is out of range; treating as standalone.");
            }
        }
        return (0, data.Length, default, false);
    }

#if NET5_0_OR_GREATER
    /// <summary>
    /// Re-signs a vbmeta image with the given PKCS#8 private key, preserving the vbmeta block offset.
    /// <para>使用给定的 PKCS#8 私钥重新签名 vbmeta 镜像，保留 vbmeta 块偏移。</para>
    /// </summary>
    /// <param name="data">Raw vbmeta image bytes. <para>原始 vbmeta 镜像字节。</para></param>
    /// <param name="privateKeyPkcs8">PKCS#8-encoded private key bytes. <para>PKCS#8 编码的私钥字节。</para></param>
    /// <param name="algorithmName">Optional algorithm name override; when null the algorithm from the header is used. <para>可选的算法名称覆盖；为 null 时使用头中的算法。</para></param>
    /// <returns>The re-signed vbmeta image bytes. <para>重新签名后的 vbmeta 镜像字节。</para></returns>
    public byte[] ReSignVbmetaImage(byte[] data, byte[] privateKeyPkcs8, string? algorithmName = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (privateKeyPkcs8 == null) throw new ArgumentNullException(nameof(privateKeyPkcs8));
        if (data.Length == 0) throw new ArgumentException("Vbmeta data is empty.", nameof(data));

        // Locate the vbmeta block (handles footered partition images where the block is not at 0).
        var (blockOffset, blockLength, _, hasFooter) = LocateVbmetaBlock(data);
        if (blockLength < AvbVBMetaImageHeader.Size)
        {
            throw new InvalidOperationException("Vbmeta image is too small to contain a valid AVB header.");
        }

        data = (byte[])data.Clone();

        // Parse the header from the vbmeta block. All header offsets (hash, signature, ...) are
        // relative to the start of the vbmeta block, so add blockOffset when writing back.
        var header = AvbVBMetaImageHeader.FromBytes(data.AsSpan(blockOffset, AvbVBMetaImageHeader.Size));

        if (header.AlgorithmType == (uint)AvbAlgorithmType.None)
        {
            throw new InvalidOperationException(
                "VBMeta uses algorithm type NONE (0); it is unsigned and cannot be re-signed. " +
                "A private key is only meaningful for signed vbmeta images.");
        }
        if (!System.Enum.IsDefined(typeof(AvbAlgorithmType), header.AlgorithmType))
        {
            throw new InvalidOperationException($"Unknown AVB algorithm type: {header.AlgorithmType}.");
        }

        var algorithm = (AvbAlgorithmType)header.AlgorithmType;

        // The hash algorithm is dictated by the header's algorithm_type (SHA256 for types 1-3,
        // SHA512 for types 4-6), never by an arbitrary caller-supplied name.
        if (!AvbCrypto.TryGetAlgorithmInfo(algorithm, out HashAlgorithmName hashAlgorithmName, out int expectedHashSize))
        {
            throw new InvalidOperationException($"No digest info available for AVB algorithm {algorithm}.");
        }

        int authBlockSize = checked((int)header.AuthenticationDataBlockSize);
        int auxBlockSize = checked((int)header.AuxiliaryDataBlockSize);
        int required = AvbVBMetaImageHeader.Size + authBlockSize + auxBlockSize;
        if (blockLength < required)
        {
            throw new InvalidOperationException(
                $"Vbmeta block size ({blockLength}) is smaller than header + auth ({required}).");
        }

        int hashOff = checked((int)header.HashOffset);
        int hashSize = checked((int)header.HashSize);
        int sigOff = checked((int)header.SignatureOffset);
        int sigSize = checked((int)header.SignatureSize);

        // All these offsets are relative to the vbmeta block start.
        if (hashSize <= 0 || hashSize != expectedHashSize ||
            hashOff < 0 || hashOff + hashSize > authBlockSize ||
            sigSize <= 0 || sigOff < 0 || sigOff + sigSize > authBlockSize)
        {
            throw new InvalidOperationException(
                $"Invalid hash/signature offset/size in vbmeta header " +
                $"(hash: off={hashOff} size={hashSize} expected={expectedHashSize}; " +
                $"sig: off={sigOff} size={sigSize}; auth block={authBlockSize}).");
        }

        // Recompute the hash over (header || auxiliary data) exactly as libavb does, because the
        // flags change invalidates the previously stored hash. The hash is written into the auth block.
        int headerAbs = blockOffset;
        int auxAbs = blockOffset + AvbVBMetaImageHeader.Size + authBlockSize;
        Span<byte> headerSpan = data.AsSpan(headerAbs, AvbVBMetaImageHeader.Size);
        Span<byte> auxSpan = data.AsSpan(auxAbs, auxBlockSize);

        byte[] newHash;
        using (var incremental = IncrementalHash.CreateHash(hashAlgorithmName))
        {
            incremental.AppendData(headerSpan.ToArray());
            incremental.AppendData(auxSpan.ToArray());
            newHash = incremental.GetHashAndReset();
        }
        newHash.AsSpan().CopyTo(data.AsSpan(blockOffset + hashOff, hashSize));

        // The AVB signature is a PKCS#1 v1.5 RSA signature over the stored hash digest itself
        // (not over the image — the digest is already the hash). Use SignHash, not SignData which
        // would double-hash.
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);
        byte[] signature = rsa.SignHash(newHash, hashAlgorithmName, RSASignaturePadding.Pkcs1);

        // Never silently truncate: the generated signature must exactly fit the declared slot.
        if (signature.Length != sigSize)
        {
            throw new InvalidOperationException(
                $"Signature length mismatch: generated {signature.Length} bytes but the vbmeta header " +
                $"declares {sigSize} bytes for algorithm {algorithm}. The supplied private key likely " +
                $"does not match the key this vbmeta was originally signed with.");
        }
        signature.AsSpan().CopyTo(data.AsSpan(blockOffset + sigOff, sigSize));

        if (!string.IsNullOrEmpty(algorithmName))
        {
            FastbootDebug.Log($"Re-signing algorithm from vbmeta header: {algorithm} ({hashAlgorithmName.Name}); " +
                              $"caller-supplied '{algorithmName}' was ignored.");
        }
        FastbootDebug.Log($"Re-signed vbmeta with {algorithm} over {hashAlgorithmName.Name}. " +
                          $"Signature size: {signature.Length} bytes" + (hasFooter ? " (footered image)" : "") + ".");

        // Verify the result structurally/cryptographically over the actual vbmeta block.
        byte[] verifyBlock = new byte[blockLength];
        Array.Copy(data, blockOffset, verifyBlock, 0, blockLength);
        var verifyResult = new AvbVBMetaImage(verifyBlock).VerifyIntegrity();
        if (verifyResult != AvbVBMetaVerifyResult.Ok)
        {
            throw new InvalidOperationException(
                $"Re-signed vbmeta failed post-signing verification: {AvbVBMetaImage.ResultToString(verifyResult)}.");
        }

        return data;
    }
#else
    /// <summary>
    /// Re-signs a vbmeta image; requires .NET 5.0 or later.
    /// <para>重新签名 vbmeta 镜像；需要 .NET 5.0 或更高版本。</para>
    /// </summary>
    /// <param name="data">Raw vbmeta image bytes. <para>原始 vbmeta 镜像字节。</para></param>
    /// <param name="privateKeyPkcs8">PKCS#8-encoded private key bytes. <para>PKCS#8 编码的私钥字节。</para></param>
    /// <param name="algorithmName">Optional algorithm name override. <para>可选的算法名称覆盖。</para></param>
    /// <returns>The re-signed vbmeta image bytes. <para>重新签名后的 vbmeta 镜像字节。</para></returns>
    public byte[] ReSignVbmetaImage(byte[] data, byte[] privateKeyPkcs8, string? algorithmName = null)
    {
        throw new NotSupportedException("Re-signing vbmeta images requires .NET 5.0 or later. Please use a newer .NET runtime.");
    }
#endif
}
