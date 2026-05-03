using FirmwareKit.AVB.Enums;

namespace FirmwareKit.Comm.Fastboot.Tests;

public class AvbVerificationServiceTests
{
    private readonly AvbVerificationService _service = new();

    private byte[] CreateMinimalVbmetaImage()
    {
        var random = new Random(42);
        var data = new byte[256];
        data[0] = (byte)'A';
        data[1] = (byte)'V';
        data[2] = (byte)'B';
        data[3] = (byte)'0';
        data[4] = 1;
        data[5] = 0;
        data[6] = 0;
        data[7] = 0;
        data[8] = 0;
        data[9] = 0;
        data[10] = 0;
        data[11] = 0;
        data[12] = 0;
        data[13] = 0;
        data[14] = 0;
        data[15] = 0;
        data[16] = 0;
        data[17] = 0;
        data[18] = 0;
        data[19] = 0;
        data[20] = 0;
        data[21] = 0;
        data[22] = 0;
        data[23] = 0;
        data[24] = 0;
        data[25] = 0;
        data[26] = 0;
        data[27] = 0;
        data[28] = 0;
        data[29] = 0;
        data[30] = 0;
        data[31] = 0;
        data[120] = 0;
        data[121] = 0;
        data[122] = 0;
        data[123] = 0;
        data[124] = 0;
        data[125] = 0;
        data[126] = 0;
        data[127] = 0;
        for (int i = 128; i < 256; i++)
        {
            data[i] = (byte)random.Next(0, 255);
        }

        return data;
    }

    [Fact]
    public void VerifyVbmetaImage_WithValidMagic_ShouldParseHeader()
    {
        var data = CreateMinimalVbmetaImage();

        var result = _service.VerifyVbmetaImage(data);

        Assert.NotNull(result);
        Assert.NotNull(result.Header);
    }

    [Fact]
    public void VerifyVbmetaImage_WithNullData_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.VerifyVbmetaImage(null!));
    }

    [Fact]
    public void VerifyVbmetaImage_WithEmptyData_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.VerifyVbmetaImage([]));
    }

    [Fact]
    public void VerifyVbmetaFile_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent.img");

        Assert.Throws<FileNotFoundException>(() => _service.VerifyVbmetaFile(nonExistentPath));
    }

    [Fact]
    public void VerifyVbmetaFile_WithNullOrEmptyPath_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.VerifyVbmetaFile(null!));
        Assert.Throws<ArgumentException>(() => _service.VerifyVbmetaFile(""));
        Assert.Throws<ArgumentException>(() => _service.VerifyVbmetaFile("   "));
    }

    [Fact]
    public void IsVbmetaImage_WithValidMagic_ShouldReturnTrue()
    {
        var data = new byte[] { (byte)'A', (byte)'V', (byte)'B', (byte)'0', 0, 0, 0, 0 };

        var result = _service.IsVbmetaImage(data);

        Assert.True(result);
    }

    [Fact]
    public void IsVbmetaImage_WithInvalidMagic_ShouldReturnFalse()
    {
        var data = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var result = _service.IsVbmetaImage(data);

        Assert.False(result);
    }

    [Fact]
    public void IsVbmetaImage_WithNullOrShortData_ShouldReturnFalse()
    {
        Assert.False(_service.IsVbmetaImage(null!));
        Assert.False(_service.IsVbmetaImage([]));
        Assert.False(_service.IsVbmetaImage(new byte[] { (byte)'A', (byte)'V' }));
    }

    [Fact]
    public void ComputeHash_WithSha256_ShouldReturnValidHash()
    {
        var data = new byte[1024];
        new Random(42).NextBytes(data);

        var result = _service.ComputeHash(data, AvbAlgorithmType.Sha256Rsa2048);

        Assert.True(result.Success);
        Assert.NotNull(result.Hash);
        Assert.Equal(32, result.Hash.Length);
    }

    [Fact]
    public void ComputeHash_WithSha512_ShouldReturnValidHash()
    {
        var data = new byte[1024];
        new Random(42).NextBytes(data);

        var result = _service.ComputeHash(data, AvbAlgorithmType.Sha512Rsa2048);

        Assert.True(result.Success);
        Assert.NotNull(result.Hash);
        Assert.Equal(64, result.Hash.Length);
    }

    [Fact]
    public void ComputeHash_WithNullData_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.ComputeHash(null!, AvbAlgorithmType.Sha256Rsa2048));
    }

    [Fact]
    public void ComputeHash_WithEmptyData_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.ComputeHash([], AvbAlgorithmType.Sha256Rsa2048));
    }

    [Fact]
    public void ComputeSaltedHash_ShouldReturnValidHash()
    {
        var data = new byte[512];
        new Random(42).NextBytes(data);
        var salt = new byte[32];
        new Random(24).NextBytes(salt);

        var result = _service.ComputeSaltedHash(data, "sha256", salt);

        Assert.True(result.Success);
        Assert.NotNull(result.Hash);
    }

    [Fact]
    public void ComputeSaltedHash_WithNullArguments_ShouldThrowArgumentNullException()
    {
        var data = new byte[] { 0x01, 0x02 };
        var salt = new byte[] { 0x03, 0x04 };

        Assert.Throws<ArgumentNullException>(() => _service.ComputeSaltedHash(null!, "sha256", salt));
        Assert.Throws<ArgumentNullException>(() => _service.ComputeSaltedHash(data, "sha256", null!));
    }

    [Fact]
    public void ComputeSaltedHash_WithEmptyAlgorithmName_ShouldThrowArgumentException()
    {
        var data = new byte[] { 0x01, 0x02 };
        var salt = new byte[] { 0x03, 0x04 };

        Assert.Throws<ArgumentException>(() => _service.ComputeSaltedHash(data, "", salt));
    }

    [Fact]
    public void ParseFooter_WithNullData_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.ParseFooter(null!));
    }

    [Fact]
    public void ParseFooter_WithShortData_ShouldReturnNull()
    {
        var data = new byte[32];

        var result = _service.ParseFooter(data);

        Assert.Null(result);
    }

    [Fact]
    public void ParseFooter_WithoutFooter_ShouldReturnNull()
    {
        var data = new byte[128];
        new Random(42).NextBytes(data);

        var result = _service.ParseFooter(data);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractDescriptors_WithNullData_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.ExtractDescriptors(null!));
    }

    [Fact]
    public void ExtractDescriptors_WithEmptyData_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.ExtractDescriptors([]));
    }

    [Fact]
    public void ExtractDescriptors_WithVbmetaImage_ShouldNotThrow()
    {
        var data = CreateMinimalVbmetaImage();

        var descriptors = _service.ExtractDescriptors(data);

        Assert.NotNull(descriptors);
    }

    [Fact]
    public void IsVbmetaFile_WithNonExistentFile_ShouldReturnFalse()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "test.img");

        Assert.False(_service.IsVbmetaFile(nonExistentPath));
    }

    [Fact]
    public void IsVbmetaFile_WithNullOrEmptyPath_ShouldReturnFalse()
    {
        Assert.False(_service.IsVbmetaFile(null!));
        Assert.False(_service.IsVbmetaFile(""));
    }

    [Fact]
    public void IsVbmetaFile_WithVbmetaFile_ShouldReturnTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "vbmeta.img");
            File.WriteAllBytes(filePath, CreateMinimalVbmetaImage());

            Assert.True(_service.IsVbmetaFile(filePath));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void HashComputation_Deterministic_SameInputSameOutput()
    {
        var data = new byte[256];
        new Random(42).NextBytes(data);

        var result1 = _service.ComputeHash(data, AvbAlgorithmType.Sha256Rsa2048);
        var result2 = _service.ComputeHash(data, AvbAlgorithmType.Sha256Rsa2048);

        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Equal(result1.Hash, result2.Hash);
    }

    [Fact]
    public void HashComputation_DifferentInputDifferentOutput()
    {
        var data1 = new byte[256];
        new Random(42).NextBytes(data1);
        var data2 = new byte[256];
        new Random(99).NextBytes(data2);

        var result1 = _service.ComputeHash(data1, AvbAlgorithmType.Sha256Rsa2048);
        var result2 = _service.ComputeHash(data2, AvbAlgorithmType.Sha256Rsa2048);

        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.NotEqual(result1.Hash, result2.Hash);
    }

    [Fact]
    public void BoundaryTestCase_OneByteData_HashShouldSucceed()
    {
        var data = new byte[] { 0x42 };

        var result = _service.ComputeHash(data, AvbAlgorithmType.Sha256Rsa2048);

        Assert.True(result.Success);
        Assert.NotNull(result.Hash);
        Assert.Equal(32, result.Hash.Length);
    }

    [Fact]
    public void BoundaryTestCase_LargeData_HashShouldSucceed()
    {
        var data = new byte[10 * 1024 * 1024];
        new Random(42).NextBytes(data);

        var result = _service.ComputeHash(data, AvbAlgorithmType.Sha256Rsa2048);

        Assert.True(result.Success);
        Assert.NotNull(result.Hash);
        Assert.Equal(32, result.Hash.Length);
    }

    [Fact]
    public void BoundaryTestCase_MinimumVbmetaSize_ShouldNotThrow()
    {
        var data = new byte[256];
        Array.Fill<byte>(data, 0);
        data[0] = (byte)'A';
        data[1] = (byte)'V';
        data[2] = (byte)'B';
        data[3] = (byte)'0';
        data[120] = 0;
        data[121] = 0;
        data[122] = 0;
        data[123] = 0;
        data[124] = 0;
        data[125] = 0;
        data[126] = 0;
        data[127] = 0;

        var result = _service.VerifyVbmetaImage(data);

        Assert.NotNull(result);
        Assert.NotNull(result.Header);
    }

    [Fact]
    public void BoundaryTestCase_LargeVbmetaImage_ShouldParse()
    {
        var data = new byte[65536];
        Array.Copy(CreateMinimalVbmetaImage(), data, 256);

        var result = _service.VerifyVbmetaImage(data);

        Assert.NotNull(result);
    }

    [Fact]
    public void ErrorHandling_VerifyVbmetaImage_CorruptedMagic_ShouldNotCrash()
    {
        var data = CreateMinimalVbmetaImage();
        data[0] = 0xFF;
        data[1] = 0xFF;

        var result = _service.VerifyVbmetaImage(data);

        Assert.NotNull(result);
        Assert.False(result.Success);
    }
}
