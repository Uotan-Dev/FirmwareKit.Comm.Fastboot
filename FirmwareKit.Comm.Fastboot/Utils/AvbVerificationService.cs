using FirmwareKit.AVB.Core;
using FirmwareKit.AVB.Descriptors;
using FirmwareKit.AVB.Enums;
using FirmwareKit.AVB.Security;
using FirmwareKit.AVB.VBMeta;

namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// Represents the result of verifying an Android Verified Boot (AVB) vbmeta image.
/// <para>表示验证 Android Verified Boot (AVB) vbmeta 镜像的结果。</para>
/// </summary>
public class AvbVerificationResult
{
    /// <summary>
    /// Gets or sets whether the verification was successful.
    /// <para>获取或设置验证是否成功。</para>
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the detailed verification result from AVB.
    /// <para>获取或设置来自 AVB 的详细验证结果。</para>
    /// </summary>
    public AvbVBMetaVerifyResult VerifyResult { get; set; }

    /// <summary>
    /// Gets or sets a human-readable message describing the verification outcome.
    /// <para>获取或设置描述验证结果的人类可读消息。</para>
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the algorithm type used for the hash.
    /// <para>获取或设置用于哈希的算法类型。</para>
    /// </summary>
    public AvbAlgorithmType AlgorithmType { get; set; }

    /// <summary>
    /// Gets or sets the hash value extracted from the vbmeta image.
    /// <para>获取或设置从 vbmeta 镜像提取的哈希值。</para>
    /// </summary>
    public byte[]? Hash { get; set; }

    /// <summary>
    /// Gets or sets the signature extracted from the vbmeta image.
    /// <para>获取或设置从 vbmeta 镜像提取的签名。</para>
    /// </summary>
    public byte[]? Signature { get; set; }

    /// <summary>
    /// Gets or sets the list of AVB descriptors found in the vbmeta image.
    /// <para>获取或设置在 vbmeta 镜像中找到的 AVB 描述符列表。</para>
    /// </summary>
    public List<AvbDescriptor> Descriptors { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a valid AVB footer was found at the end of the image.
    /// <para>获取或设置是否在镜像末尾找到有效的 AVB footer。</para>
    /// </summary>
    public bool HasFooter { get; set; }

    /// <summary>
    /// Gets or sets the AVB footer if present.
    /// <para>获取或设置 footer（如果存在）。</para>
    /// </summary>
    public AvbFooter? Footer { get; set; }

    /// <summary>
    /// Gets or sets the vbmeta image header.
    /// <para>获取或设置 vbmeta 镜像头。</para>
    /// </summary>
    public AvbVBMetaImageHeader? Header { get; set; }
}

/// <summary>
/// Represents the result of computing a hash using AVB algorithms.
/// <para>表示使用 AVB 算法计算哈希的结果。</para>
/// </summary>
public class AvbHashResult
{
    /// <summary>
    /// Gets or sets whether the hash computation was successful.
    /// <para>获取或设置哈希计算是否成功。</para>
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the computed hash value.
    /// <para>获取或设置计算的哈希值。</para>
    /// </summary>
    public byte[]? Hash { get; set; }

    /// <summary>
    /// Gets or sets the name of the algorithm used.
    /// <para>获取或设置所使用的算法名称。</para>
    /// </summary>
    public string AlgorithmName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a human-readable message describing the operation outcome.
    /// <para>获取或设置描述操作结果的人类可读消息。</para>
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Represents the result of verifying a signature.
/// <para>表示验证签名的结果。</para>
/// </summary>
public class AvbSignatureResult
{
    /// <summary>
    /// Gets or sets whether the signature verification was successful.
    /// <para>获取或设置签名验证是否成功。</para>
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the signature that was verified.
    /// <para>获取或设置已验证的签名。</para>
    /// </summary>
    public byte[]? Signature { get; set; }

    /// <summary>
    /// Gets or sets the name of the algorithm used for the signature.
    /// <para>获取或设置用于签名的算法名称。</para>
    /// </summary>
    public string AlgorithmName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a human-readable message describing the verification outcome.
    /// <para>获取或设置描述验证结果的人类可读消息。</para>
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Provides AVB (Android Verified Boot) verification services for vbmeta images.
/// <para>为 vbmeta 镜像提供 AVB (Android Verified Boot) 验证服务。</para>
/// </summary>
public class AvbVerificationService
{
    /// <summary>
    /// Verifies the integrity of an AVB vbmeta image from raw bytes.
    /// <para>从原始字节验证 AVB vbmeta 镜像的完整性。</para>
    /// </summary>
    /// <param name="data">The raw vbmeta image data. <para>原始 vbmeta 镜像数据。</para></param>
    /// <returns>An AvbVerificationResult containing the verification outcome. <para>包含验证结果的 AvbVerificationResult。</para></returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null. <para>当 data 为 null 时抛出。</para></exception>
    /// <exception cref="ArgumentException">Thrown when data is empty. <para>当 data 为空时抛出。</para></exception>
    public AvbVerificationResult VerifyVbmetaImage(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("Vbmeta data is empty.", nameof(data));

        var result = new AvbVerificationResult();

        try
        {
            var vbmetaImage = new AvbVBMetaImage(data);
            result.Header = vbmetaImage.Header;

            result.VerifyResult = vbmetaImage.VerifyIntegrity();
            result.Success = result.VerifyResult == AvbVBMetaVerifyResult.Ok;

            if (result.Success)
            {
                result.Message = "VBMeta image verification succeeded.";
            }
            else
            {
                result.Message = $"VBMeta image verification failed: {AvbVBMetaImage.ResultToString(result.VerifyResult)}";
            }

            result.AlgorithmType = (AvbAlgorithmType)vbmetaImage.Header.AlgorithmType;

            if (vbmetaImage.Header.HashSize > 0)
            {
                result.Hash = new byte[vbmetaImage.Header.HashSize];
                var hashSpan = data.AsSpan((int)vbmetaImage.Header.HashOffset, (int)vbmetaImage.Header.HashSize);
                hashSpan.CopyTo(result.Hash);
            }

            if (vbmetaImage.Header.SignatureSize > 0)
            {
                result.Signature = new byte[vbmetaImage.Header.SignatureSize];
                var sigSpan = data.AsSpan((int)vbmetaImage.Header.SignatureOffset, (int)vbmetaImage.Header.SignatureSize);
                sigSpan.CopyTo(result.Signature);
            }

            try
            {
                result.Descriptors = vbmetaImage.GetAllDescriptors().ToList();
            }
            catch (Exception ex)
            {
                FastbootDebug.Log($"Descriptor extraction warning: {ex.Message}");
                result.Descriptors = [];
            }

            result.HasFooter = false;
            if (data.Length >= (int)AvbFooter.Size)
            {
                var footerSpan = data.AsSpan(data.Length - (int)AvbFooter.Size, (int)AvbFooter.Size);
                if (AvbFooter.TryFromBytes(footerSpan, out var footer) && footer.IsValid)
                {
                    result.HasFooter = true;
                    result.Footer = footer;
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Failed to parse or verify VBMeta image: {ex.Message}";
            FastbootDebug.Log($"VBMeta verification error: {ex}");
        }

        return result;
    }

    /// <summary>
    /// Verifies the integrity of an AVB vbmeta image from a file.
    /// <para>从文件验证 AVB vbmeta 镜像的完整性。</para>
    /// </summary>
    /// <param name="filePath">The path to the vbmeta image file. <para>vbmeta 镜像文件的路径。</para></param>
    /// <returns>An AvbVerificationResult containing the verification outcome. <para>包含验证结果的 AvbVerificationResult。</para></returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace. <para>当 filePath 为 null 或空白时抛出。</para></exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist. <para>当文件不存在时抛出。</para></exception>
    /// <exception cref="IOException">Thrown when the file cannot be read. <para>当文件无法读取时抛出。</para></exception>
    public AvbVerificationResult VerifyVbmetaFile(string filePath)
    {
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

        return VerifyVbmetaImage(data);
    }

    /// <summary>
    /// Computes a hash of the data using the specified AVB algorithm type.
    /// <para>使用指定的 AVB 算法类型计算数据的哈希值。</para>
    /// </summary>
    /// <param name="data">The data to hash. <para>要哈希的数据。</para></param>
    /// <param name="algorithmType">The AVB algorithm type to use. <para>要使用的 AVB 算法类型。</para></param>
    /// <returns>An AvbHashResult containing the computed hash. <para>包含计算哈希结果的 AvbHashResult。</para></returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null. <para>当 data 为 null 时抛出。</para></exception>
    /// <exception cref="ArgumentException">Thrown when data is empty. <para>当 data 为空时抛出。</para></exception>
    public AvbHashResult ComputeHash(byte[] data, AvbAlgorithmType algorithmType)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("Data is empty.", nameof(data));

        var result = new AvbHashResult();

        try
        {
            result.Hash = AvbCrypto.CalculateHash(algorithmType, data);
            result.Success = result.Hash != null && result.Hash.Length > 0;

            if (result.Success)
            {
                result.AlgorithmName = algorithmType.ToString();
                result.Message = $"Hash computed successfully using {result.AlgorithmName}.";
            }
            else
            {
                result.Message = "Hash computation returned empty result.";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Hash computation failed: {ex.Message}";
            FastbootDebug.Log($"Hash computation error: {ex}");
        }

        return result;
    }

    /// <summary>
    /// Computes a salted hash of the data using the specified algorithm name and salt.
    /// <para>使用指定的算法名称和盐值计算数据的加盐哈希值。</para>
    /// </summary>
    /// <param name="data">The data to hash. <para>要哈希的数据。</para></param>
    /// <param name="algorithmName">The name of the hash algorithm to use. <para>要使用的哈希算法名称。</para></param>
    /// <param name="salt">The salt value to use for hashing. <para>用于哈希的盐值。</para></param>
    /// <returns>An AvbHashResult containing the computed salted hash. <para>包含计算加盐哈希结果的 AvbHashResult。</para></returns>
    /// <exception cref="ArgumentNullException">Thrown when data, algorithmName, or salt is null. <para>当 data、algorithmName 或 salt 为 null 时抛出。</para></exception>
    /// <exception cref="ArgumentException">Thrown when data is empty or algorithmName is whitespace. <para>当 data 为空或 algorithmName 为空白时抛出。</para></exception>
    public AvbHashResult ComputeSaltedHash(byte[] data, string algorithmName, byte[] salt)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("Data is empty.", nameof(data));
        if (string.IsNullOrWhiteSpace(algorithmName)) throw new ArgumentException("Algorithm name is required.", nameof(algorithmName));
        if (salt == null) throw new ArgumentNullException(nameof(salt));

        var result = new AvbHashResult();

        try
        {
            result.Hash = AvbCrypto.CalculateHash(algorithmName, data, salt);
            result.Success = result.Hash != null && result.Hash.Length > 0;
            result.AlgorithmName = algorithmName;
            result.Message = result.Success
                ? $"Salted hash computed successfully using {algorithmName}."
                : "Salted hash computation returned empty result.";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Salted hash computation failed: {ex.Message}";
            FastbootDebug.Log($"Salted hash computation error: {ex}");
        }

        return result;
    }

    /// <summary>
    /// Verifies a signature against data using a public key.
    /// <para>使用公钥验证数据上的签名。</para>
    /// </summary>
    /// <param name="data">The data whose signature is being verified. <para>正在验证签名的数据。</para></param>
    /// <param name="signature">The signature to verify. <para>要验证的签名。</para></param>
    /// <param name="publicKey">The RSA public key used for verification. <para>用于验证的 RSA 公钥。</para></param>
    /// <returns>An AvbSignatureResult containing the verification outcome. <para>包含验证结果的 AvbSignatureResult。</para></returns>
    /// <exception cref="ArgumentNullException">Thrown when data, signature, or publicKey is null. <para>当 data、signature 或 publicKey 为 null 时抛出。</para></exception>
    public AvbSignatureResult VerifySignature(byte[] data, byte[] signature, byte[] publicKey)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (signature == null) throw new ArgumentNullException(nameof(signature));
        if (publicKey == null) throw new ArgumentNullException(nameof(publicKey));

        var result = new AvbSignatureResult();

        try
        {
            using var rsa = System.Security.Cryptography.RSA.Create();
            var rsaParams = AvbCrypto.ParseRSAPublicKey(publicKey);
            rsa.ImportParameters(rsaParams);

            var algorithmType = AvbCrypto.GetAlgorithmType("sha256");
            var hash = AvbCrypto.CalculateHash(algorithmType, data);

            var deformatter = new System.Security.Cryptography.RSAPKCS1SignatureDeformatter(rsa);
            deformatter.SetHashAlgorithm("SHA256");

            result.Success = deformatter.VerifySignature(hash, signature);
            result.Signature = signature;
            result.AlgorithmName = algorithmType.ToString();
            result.Message = result.Success
                ? "Signature verification succeeded."
                : "Signature verification failed: signature does not match.";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Signature verification failed: {ex.Message}";
            FastbootDebug.Log($"Signature verification error: {ex}");
        }

        return result;
    }

    /// <summary>
    /// Parses and extracts an AVB footer from the end of a vbmeta image.
    /// <para>从 vbmeta 镜像末尾解析并提取 AVB footer。</para>
    /// </summary>
    /// <param name="data">The raw vbmeta image data. <para>原始 vbmeta 镜像数据。</para></param>
    /// <returns>The AvbFooter if present and valid, otherwise null. <para>如果存在且有效则返回 AvbFooter，否则返回 null。</para></returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null. <para>当 data 为 null 时抛出。</para></exception>
    public AvbFooter? ParseFooter(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length < (int)AvbFooter.Size) return null;

        var footerSpan = data.AsSpan(data.Length - (int)AvbFooter.Size, (int)AvbFooter.Size);
        if (AvbFooter.TryFromBytes(footerSpan, out var footer) && footer.IsValid)
        {
            return footer;
        }

        return null;
    }

    /// <summary>
    /// Extracts all AVB descriptors from a vbmeta image.
    /// <para>从 vbmeta 镜像提取所有 AVB 描述符。</para>
    /// </summary>
    /// <param name="data">The raw vbmeta image data. <para>原始 vbmeta 镜像数据。</para></param>
    /// <returns>A list of AvbDescriptor objects found in the image. <para>在镜像中找到的 AvbDescriptor 对象列表。</para></returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null. <para>当 data 为 null 时抛出。</para></exception>
    /// <exception cref="ArgumentException">Thrown when data is empty. <para>当 data 为空时抛出。</para></exception>
    public List<AvbDescriptor> ExtractDescriptors(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("Vbmeta data is empty.", nameof(data));

        try
        {
            var vbmetaImage = new AvbVBMetaImage(data);
            return vbmetaImage.GetAllDescriptors().ToList();
        }
        catch (Exception ex)
        {
            FastbootDebug.Log($"Descriptor extraction error: {ex}");
            return [];
        }
    }

    /// <summary>
    /// Determines whether the given data appears to be a valid AVB vbmeta image.
    /// <para>判断给定的数据是否看起来是有效的 AVB vbmeta 镜像。</para>
    /// </summary>
    /// <param name="data">The data to check. <para>要检查的数据。</para></param>
    /// <returns>True if the data starts with the AVB magic bytes "AVB0"; otherwise, false. <para>如果数据以 AVB 魔术字节 "AVB0" 开头则返回 true；否则返回 false。</para></returns>
    public bool IsVbmetaImage(byte[] data)
    {
        if (data == null || data.Length < 4) return false;
        return data[0] == (byte)'A' && data[1] == (byte)'V' && data[2] == (byte)'B' && data[3] == (byte)'0';
    }

    /// <summary>
    /// Determines whether the specified file appears to be a valid AVB vbmeta image.
    /// <para>判断指定文件是否看起来是有效的 AVB vbmeta 镜像。</para>
    /// </summary>
    /// <param name="filePath">The path to the file to check. <para>要检查的文件的路径。</para></param>
    /// <returns>True if the file exists and starts with the AVB magic bytes "AVB0"; otherwise, false. <para>如果文件存在且以 AVB 魔术字节 "AVB0" 开头则返回 true；否则返回 false。</para></returns>
    public bool IsVbmetaFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;

        try
        {
            using var fs = File.OpenRead(filePath);
            byte[] magic = new byte[4];
            if (fs.Read(magic, 0, 4) == 4)
            {
                return magic[0] == (byte)'A' && magic[1] == (byte)'V' && magic[2] == (byte)'B' && magic[3] == (byte)'0';
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}