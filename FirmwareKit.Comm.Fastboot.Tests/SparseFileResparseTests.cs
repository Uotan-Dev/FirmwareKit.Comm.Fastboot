using FirmwareKit.Sparse.Core;
using FirmwareKit.Sparse.Models;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class SparseFileResparseTests
    {
        [Fact]
        public async Task ResparseAndMerge_ShouldProduceIdenticalSparseImage()
        {
            // Arrange: 创建一个临时稀疏文件
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var originalPath = Path.Combine(tempDir, "original_sparse.img");
            var mergedPath = Path.Combine(tempDir, "merged_sparse.img");
            var blockSize = 4096u;
            var totalSize = 10 * 1024 * 1024; // 10MB
            using (var sparse = new SparseFile(blockSize, totalSize))
            {
                // 填充一些数据块
                sparse.AddFillChunk(0xAABBCCDD, 2 * 1024 * 1024); // 2MB填充
                sparse.AddDontCareChunk(1 * 1024 * 1024); // 1MB空洞
                sparse.AddRawChunk(new byte[blockSize * 10]); // 10块原始数据
                using (var fs = File.Create(originalPath))
                {
                    sparse.WriteToStream(fs, sparse: true);
                }
            }

            // Act: 读取稀疏文件并重稀疏化再合并
            using var original = SparseFile.FromImageFile(originalPath);
            var parts = original.Resparse(2 * 1024 * 1024).ToArray(); // 2MB分块

            // 合并所有分块为一个新的SparseFile对象，使用公开API重建chunk
            var mergedSparse = new SparseFile(parts[0].Header.BlockSize, original.Header.TotalBlocks * parts[0].Header.BlockSize);
            foreach (var part in parts)
            {
                foreach (var chunk in part.Chunks)
                {
                    switch ((ChunkType)chunk.Header.ChunkType)
                    {
                        case ChunkType.Raw:
                            if (chunk.DataProvider != null)
                            {
                                var buffer = new byte[chunk.Header.ChunkSize * mergedSparse.Header.BlockSize];
                                chunk.DataProvider.Read(0, buffer, 0, buffer.Length);
                                mergedSparse.AddRawChunk(buffer);
                            }
                            break;
                        case ChunkType.Fill:
                            mergedSparse.AddFillChunk(chunk.FillValue, chunk.Header.ChunkSize * mergedSparse.Header.BlockSize);
                            break;
                        case ChunkType.DontCare:
                            mergedSparse.AddDontCareChunk(chunk.Header.ChunkSize * mergedSparse.Header.BlockSize);
                            break;
                    }
                }
                part.Dispose();
            }
            // 合并后补齐DONT_CARE块，确保总块数一致
            if (mergedSparse.CurrentBlock < mergedSparse.Header.TotalBlocks)
            {
                var remainBlocks = mergedSparse.Header.TotalBlocks - mergedSparse.CurrentBlock;
                mergedSparse.AddDontCareChunk(remainBlocks * mergedSparse.Header.BlockSize);
            }
            using (var fs = File.Create(mergedPath))
            {
                mergedSparse.WriteToStream(fs, sparse: true);
            }
            mergedSparse.Dispose();

            // Assert: 解包为raw image后比对内容
            var originalRaw = Path.Combine(tempDir, "original.raw");
            var mergedRaw = Path.Combine(tempDir, "merged.raw");
            using (var origSparse = SparseFile.FromImageFile(originalPath))
            using (var origRaw = File.Create(originalRaw))
                origSparse.WriteRawToStream(origRaw);

            using (var mergedSparseFile = SparseFile.FromImageFile(mergedPath))
            using (var mergedRawStream = File.Create(mergedRaw))
                mergedSparseFile.WriteRawToStream(mergedRawStream);

            var originalRawBytes = await File.ReadAllBytesAsync(originalRaw);
            var mergedRawBytes = await File.ReadAllBytesAsync(mergedRaw);
            Assert.Equal(originalRawBytes, mergedRawBytes);

            // 清理
            File.Delete(originalPath);
            File.Delete(mergedPath);
            File.Delete(originalRaw);
            File.Delete(mergedRaw);
            Directory.Delete(tempDir);
        }
    }
}
