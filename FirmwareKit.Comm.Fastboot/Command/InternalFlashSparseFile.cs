namespace FirmwareKit.Comm.Fastboot;

using FirmwareKit.Sparse.Core;
using FirmwareKit.Sparse.Streams;

public partial class FastbootDriver
{
    private static uint FindMaxBlockCountWithinLimit(SparseFile sparseFile, uint startBlock, uint maxBlocks, long limit)
    {
        uint low = 1;
        uint high = maxBlocks;
        uint best = 0;

        while (low <= high)
        {
            uint mid = low + ((high - low) / 2);
            using var probe = new SparseImageStream(sparseFile, startBlock, mid, includeCrc: false, fullRange: true, disposeSource: false);
            if (probe.Length <= limit)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }

    /// <summary>
    /// Flashes a sparse image file to the specified partition.
    /// Automatically chunks the sparse image if it exceeds the max download size.
    /// <para>将稀疏镜像文件刷写到指定分区。
    /// 如果稀疏镜像超过最大下载大小，则自动分块。</para>
    /// </summary>
    /// <param name="partition">Target partition to flash (e.g., "system"). <para>目标刷写分区（如 "system"）。</param>
    /// <param name="sparseFile">The sparse file to flash. <para>要刷写的稀疏文件。</param>
    /// <param name="maxDownloadSize">Maximum download size in bytes. Use 0 for device default. <para>最大下载大小（字节）。使用 0 表示设备默认值。</param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse FlashSparseFile(string partition, SparseFile sparseFile, long maxDownloadSize)
    {
        if (sparseFile == null) throw new ArgumentNullException(nameof(sparseFile));

        long limit = maxDownloadSize > 0 ? maxDownloadSize : GetMaxDownloadSize();
        if (limit <= 0)
        {
            return new FastbootResponse { Result = FastbootState.Fail, Response = "invalid sparse limit" };
        }

        uint totalBlocks = sparseFile.Header.TotalBlocks;
        uint startBlock = 0;
        var partIndex = 0;
        FastbootResponse last = new FastbootResponse { Result = FastbootState.Success };

        while (startBlock < totalBlocks)
        {
            uint remainingBlocks = totalBlocks - startBlock;
            uint blockCount = FindMaxBlockCountWithinLimit(sparseFile, startBlock, remainingBlocks, limit);

            if (blockCount == 0)
            {
                if (partIndex == 0 && startBlock == 0)
                {
                    using var singleSparseStream = new SparseImageStream(sparseFile, 0, totalBlocks, includeCrc: false, fullRange: true, disposeSource: false);
                    long singleLength = singleSparseStream.Length;
                    var swSingle = System.Diagnostics.Stopwatch.StartNew();
                    var singleDownload = DownloadData(singleSparseStream, singleLength);
                    swSingle.Stop();
                    OnStepFinished?.Invoke($"Flash {partition} (single)", swSingle.Elapsed, singleDownload.Result == FastbootState.Success);
                    if (singleDownload.Result != FastbootState.Success)
                        return singleDownload;
                    NotifyCurrentStep($"Flashing {partition} (single)...");
                    var swFlash = System.Diagnostics.Stopwatch.StartNew();
                    last = RawCommand("flash:" + partition);
                    swFlash.Stop();
                    OnStepFinished?.Invoke($"Flash {partition} (single) commit", swFlash.Elapsed, last.Result == FastbootState.Success);
                    if (last.Result != FastbootState.Success)
                        return last;
                    return last;
                }

                return new FastbootResponse
                {
                    Result = FastbootState.Fail,
                    Response = "sparse limit too small to create next part"
                };
            }

            NotifyCurrentStep($"Sending sparse image {partIndex + 1} to {partition}...");
            using var sparseStream = new SparseImageStream(sparseFile, startBlock, blockCount, includeCrc: false, fullRange: true, disposeSource: false);
            long sparseLength = sparseStream.Length;

            var swDownload = System.Diagnostics.Stopwatch.StartNew();
            var download = DownloadData(sparseStream, sparseLength);
            swDownload.Stop();
            OnStepFinished?.Invoke($"Flash {partition} block {partIndex + 1} download", swDownload.Elapsed, download.Result == FastbootState.Success);
            if (download.Result != FastbootState.Success)
            {
                return download;
            }

            NotifyCurrentStep($"Flashing {partition} ({partIndex + 1})...");
            var swFlashBlock = System.Diagnostics.Stopwatch.StartNew();
            last = RawCommand("flash:" + partition);
            swFlashBlock.Stop();
            OnStepFinished?.Invoke($"Flash {partition} block {partIndex + 1} commit", swFlashBlock.Elapsed, last.Result == FastbootState.Success);
            if (last.Result != FastbootState.Success)
            {
                return last;
            }

            startBlock += blockCount;
            partIndex++;
        }

        if (partIndex == 0)
        {
            return new FastbootResponse { Result = FastbootState.Fail, Response = "sparse image contains no blocks" };
        }

        return last;
    }


}






