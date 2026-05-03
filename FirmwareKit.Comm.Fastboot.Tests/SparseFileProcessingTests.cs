using FirmwareKit.Sparse.Core;
using FirmwareKit.Sparse.Models;
using FirmwareKit.Sparse.Streams;
using FirmwareKit.Sparse.Utils;

namespace FirmwareKit.Comm.Fastboot.Tests;

public class SparseFileProcessingTests
{
    [Fact]
    public void CreateSparseFile_WithValidParameters_ShouldSucceed()
    {
        using var sparse = new SparseFile(4096, 10 * 1024 * 1024);

        Assert.NotNull(sparse);
        Assert.Equal(4096u, sparse.Header.BlockSize);
        Assert.True(sparse.Header.TotalBlocks > 0);
    }

    [Fact]
    public void CreateSparseFile_WithZeroBlockSize_ShouldThrowException()
    {
        Assert.ThrowsAny<Exception>(() => new SparseFile(0, 1024));
    }

    [Fact]
    public void CreateSparseFile_MinimalTotalSize_CreatesValidSparse()
    {
        using var sparse = new SparseFile(4096, 4096);

        Assert.NotNull(sparse);
        Assert.Equal(4096u, sparse.Header.BlockSize);
    }

    [Fact]
    public void AddRawChunk_ShouldIncreaseChunkCount()
    {
        using var sparse = new SparseFile(4096, 10 * 1024 * 1024);
        var rawData = new byte[4096 * 10];
        new Random(42).NextBytes(rawData);

        sparse.AddRawChunk(rawData);

        Assert.True(sparse.Chunks.Count >= 1);
    }

    [Fact]
    public void AddFillChunk_ShouldIncreaseChunkCount()
    {
        using var sparse = new SparseFile(4096, 10 * 1024 * 1024);

        sparse.AddFillChunk(0xAABBCCDD, 4096 * 10);

        Assert.True(sparse.Chunks.Count >= 1);
    }

    [Fact]
    public void AddDontCareChunk_ShouldIncreaseChunkCount()
    {
        using var sparse = new SparseFile(4096, 10 * 1024 * 1024);

        sparse.AddDontCareChunk(4096 * 10);

        Assert.True(sparse.Chunks.Count >= 1);
    }

    [Fact]
    public void WriteToStream_AsSparse_ShouldProduceValidSparseFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test_sparse.img");
            using var sparse = new SparseFile(4096, 2 * 1024 * 1024);
            sparse.AddRawChunk(new byte[4096 * 10]);
            sparse.AddFillChunk(0xAABBCCDD, 4096 * 5);
            sparse.AddDontCareChunk(4096 * 20);

            using (var fs = File.Create(filePath))
            {
                sparse.WriteToStream(fs, sparse: true);
            }

            Assert.True(File.Exists(filePath));
            Assert.True(new FileInfo(filePath).Length > 0);

            Assert.True(SparseImageValidator.IsSparseImage(filePath));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void WriteToStream_AsRaw_ShouldProduceRawImage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test_raw.img");
            var expectedSize = 2 * 1024 * 1024;
            using var sparse = new SparseFile(4096, expectedSize);
            var rawData = new byte[4096 * 10];
            new Random(42).NextBytes(rawData);
            sparse.AddRawChunk(rawData);
            sparse.AddDontCareChunk(expectedSize - rawData.Length);

            using (var fs = File.Create(filePath))
            {
                sparse.WriteRawToStream(fs);
            }

            Assert.True(File.Exists(filePath));
            Assert.Equal(expectedSize, new FileInfo(filePath).Length);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void SparseFile_FromImageFile_ShouldParseCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test_sparse.img");
            using var sparse = new SparseFile(4096, 2 * 1024 * 1024);
            sparse.AddRawChunk(new byte[4096 * 10]);
            sparse.AddFillChunk(0xAABBCCDD, 4096 * 5);

            using (var fs = File.Create(filePath))
            {
                sparse.WriteToStream(fs, sparse: true);
            }

            using var parsed = SparseFile.FromImageFile(filePath);

            Assert.NotNull(parsed);
            Assert.Equal(4096u, parsed.Header.BlockSize);
            Assert.True(parsed.Chunks.Count >= 1);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void SparseStream_ShouldProduceCorrectRawData()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test_sparse.img");
            using var sparse = new SparseFile(4096, 1 * 1024 * 1024);
            sparse.AddRawChunk(new byte[4096 * 10]);
            sparse.AddFillChunk(0xAABBCCDD, 4096 * 5);

            using (var fs = File.Create(filePath))
            {
                sparse.WriteToStream(fs, sparse: true);
            }

            using var parsed = SparseFile.FromImageFile(filePath);
            using var sparseStream = new SparseStream(parsed);

            var buffer = new byte[4096];
            int totalRead = 0;
            int read;
            while ((read = sparseStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalRead += read;
            }

            Assert.Equal(1 * 1024 * 1024, totalRead);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task SparseImageConverter_RoundTrip_ShouldPreserveData()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var rawPath = Path.Combine(tempDir, "test_raw.img");
            var sparsePath = Path.Combine(tempDir, "test_sparse.img");
            var roundTripPath = Path.Combine(tempDir, "test_roundtrip.img");

            var rawData = new byte[1 * 1024 * 1024];
            new Random(42).NextBytes(rawData);
            await File.WriteAllBytesAsync(rawPath, rawData, TestContext.Current.CancellationToken);

            await SparseImageConverter.ConvertRawToSparseAsync(rawPath, sparsePath, 4096, TestContext.Current.CancellationToken);
            Assert.True(File.Exists(sparsePath));

            await SparseImageConverter.ConvertSparseToRawAsync(new[] { sparsePath }, roundTripPath, TestContext.Current.CancellationToken);
            Assert.True(File.Exists(roundTripPath));

            var rawBytes = await File.ReadAllBytesAsync(rawPath, TestContext.Current.CancellationToken);
            var roundTripBytes = await File.ReadAllBytesAsync(roundTripPath, TestContext.Current.CancellationToken);

            Assert.Equal(rawBytes.Length, roundTripBytes.Length);
            Assert.Equal(rawBytes, roundTripBytes);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void ResparseImage_ShouldSplitCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var inputPath = Path.Combine(tempDir, "large_sparse.img");
            var outputPattern = Path.Combine(tempDir, "part_{0}.img");
            var blockSize = 4096u;
            var maxFileSize = 256 * 1024;

            using var sparse = new SparseFile(blockSize, 2 * 1024 * 1024);
            sparse.AddRawChunk(new byte[blockSize * 200]);
            sparse.AddDontCareChunk(blockSize * 100);

            using (var fs = File.Create(inputPath))
            {
                sparse.WriteToStream(fs, sparse: true);
            }

            SparseImageConverter.ResparseImage(inputPath, outputPattern, maxFileSize);

            Assert.True(File.Exists(Path.Combine(tempDir, "part_0.img")));
            Assert.True(File.Exists(Path.Combine(tempDir, "part_1.img")));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void ValidateSparseImage_WithValidImage_ShouldReturnValid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test_sparse.img");
            using var sparse = new SparseFile(4096, 2 * 1024 * 1024);
            sparse.AddRawChunk(new byte[4096 * 10]);

            using (var fs = File.Create(filePath))
            {
                sparse.WriteToStream(fs, sparse: true);
            }

            var result = SparseImageValidator.ValidateSparseImage(filePath);

            Assert.NotNull(result);
            Assert.True(result.Success);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void ValidateSparseImage_WithNonSparseFile_ShouldReturnInvalid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "not_sparse.bin");
            var data = new byte[1024];
            new Random(42).NextBytes(data);
            File.WriteAllBytes(filePath, data);

            var result = SparseImageValidator.ValidateSparseImage(filePath);

            Assert.NotNull(result);
            Assert.False(result.Success);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void GetSparseImageInfo_ShouldReturnMetadata()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test_sparse.img");
            using var sparse = new SparseFile(4096, 2 * 1024 * 1024);
            sparse.AddRawChunk(new byte[4096 * 10]);
            sparse.AddFillChunk(0xAABBCCDD, 4096 * 5);

            using (var fs = File.Create(filePath))
            {
                sparse.WriteToStream(fs, sparse: true);
            }

            var info = SparseImageValidator.GetSparseImageInfo(filePath);

            Assert.NotNull(info);
            Assert.True(info.UncompressedSize > 0);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void CompareFiles_WithIdenticalFiles_ShouldShowEqual()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test.img");
            var data = new byte[4096];
            new Random(42).NextBytes(data);
            File.WriteAllBytes(filePath, data);

            var result = SparseImageUtils.CompareFiles(filePath, filePath);

            Assert.NotNull(result);
            Assert.True(result.SizeMatches);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void CompareFiles_WithDifferentFiles_ShouldShowDifferent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath1 = Path.Combine(tempDir, "test1.img");
            var filePath2 = Path.Combine(tempDir, "test2.img");
            var data1 = new byte[] { 0x01, 0x02, 0x03 };
            var data2 = new byte[] { 0x04, 0x05, 0x06, 0x07 };

            File.WriteAllBytes(filePath1, data1);
            File.WriteAllBytes(filePath2, data2);

            var result = SparseImageUtils.CompareFiles(filePath1, filePath2);

            Assert.NotNull(result);
            Assert.False(result.SizeMatches);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void CreateTestSparseImage_ShouldSucceed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test_sparse.img");

            var result = SparseImageUtils.CreateTestSparseImage(filePath, 1, 4096);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void CreateTestSparseImage_WithSmallSize_ShouldSucceed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test_sparse.img");

            var result = SparseImageUtils.CreateTestSparseImage(filePath, 1, 512);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void SparseFile_ImportAuto_ShouldDetectSparseFormat()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "test_sparse.img");
            using var sparse = new SparseFile(4096, 1 * 1024 * 1024);
            sparse.AddRawChunk(new byte[4096 * 10]);
            sparse.AddFillChunk(0xAABBCCDD, 4096 * 5);

            using (var fs = File.Create(filePath))
            {
                sparse.WriteToStream(fs, sparse: true);
            }

            using var result = SparseFile.ImportAuto(filePath, validateCrc: false, verbose: false);

            Assert.NotNull(result);
            Assert.Equal(4096u, result.Header.BlockSize);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void SparseFile_ChunkTypes_CorrectlyIdentified()
    {
        using var sparse = new SparseFile(4096, 10 * 1024 * 1024);
        sparse.AddRawChunk(new byte[4096 * 5]);
        sparse.AddFillChunk(0xAABBCCDD, 4096 * 10);
        sparse.AddDontCareChunk(4096 * 20);
        sparse.AddRawChunk(new byte[4096 * 3]);

        Assert.Equal(4, sparse.Chunks.Count);

        Assert.Equal(ChunkType.Raw, (ChunkType)sparse.Chunks[0].Header.ChunkType);
        Assert.Equal(ChunkType.Fill, (ChunkType)sparse.Chunks[1].Header.ChunkType);
        Assert.Equal(ChunkType.DontCare, (ChunkType)sparse.Chunks[2].Header.ChunkType);
        Assert.Equal(ChunkType.Raw, (ChunkType)sparse.Chunks[3].Header.ChunkType);
    }

    [Fact]
    public void BoundaryTestCase_EmptyRawChunk_ShouldNotCrash()
    {
        using var sparse = new SparseFile(4096, 4096 * 10);

        var ex = Record.Exception(() => sparse.AddRawChunk([]));

        Assert.Null(ex);
    }

    [Fact]
    public void BoundaryTestCase_MaximumBlockSize_SparseCreation()
    {
        using var sparse = new SparseFile(65536, 1 * 1024 * 1024);
        sparse.AddRawChunk(new byte[65536 * 5]);

        Assert.Equal(65536u, sparse.Header.BlockSize);
        Assert.True(sparse.Chunks.Count >= 1);
    }

    [Fact]
    public void StressTestCase_LargeSparseFile_WriteAndRead()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "large_sparse.img");
            var totalSize = 50 * 1024 * 1024; // 50MB
            using var sparse = new SparseFile(4096, totalSize);

            for (int i = 0; i < 100; i++)
            {
                var rawData = new byte[4096 * 10];
                new Random(i).NextBytes(rawData);
                sparse.AddRawChunk(rawData);
            }
            sparse.AddDontCareChunk(sparse.Header.TotalBlocks * sparse.Header.BlockSize - sparse.CurrentBlock * sparse.Header.BlockSize);

            using (var fs = File.Create(filePath))
            {
                sparse.WriteToStream(fs, sparse: true);
            }

            using var parsed = SparseFile.FromImageFile(filePath);
            using var sparseStream = new SparseStream(parsed);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;
            while ((read = sparseStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalRead += read;
            }

            Assert.Equal((long)totalSize, totalRead);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
