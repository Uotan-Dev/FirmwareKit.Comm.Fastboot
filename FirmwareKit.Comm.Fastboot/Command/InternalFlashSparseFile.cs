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
                // Keep compatibility with previous fallback behavior when limit is unrealistically small.
                if (partIndex == 0 && startBlock == 0)
                {
                    using var singleSparseStream = new SparseImageStream(sparseFile, 0, totalBlocks, includeCrc: false, fullRange: true, disposeSource: false);
                    long singleLength = singleSparseStream.Length;
                    var singleDownload = DownloadData(singleSparseStream, singleLength);
                    if (singleDownload.Result != FastbootState.Success)
                        return singleDownload;
                    NotifyCurrentStep($"Flashing {partition} (single)...");
                    last = RawCommand("flash:" + partition);
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

            var download = DownloadData(sparseStream, sparseLength);
            if (download.Result != FastbootState.Success)
            {
                return download;
            }

            NotifyCurrentStep($"Flashing {partition} ({partIndex + 1})...");
            last = RawCommand("flash:" + partition);
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






