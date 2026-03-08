namespace FirmwareKit.Comm.Fastboot;

using FirmwareKit.Sparse.Core;
using FirmwareKit.Sparse.Streams;

public partial class FastbootDriver
{
    public FastbootResponse FlashSparseFile(string partition, SparseFile sparseFile, long maxDownloadSize)
    {
        if (sparseFile == null) throw new ArgumentNullException(nameof(sparseFile));

        long limit = maxDownloadSize > 0 ? maxDownloadSize : GetMaxDownloadSize();
        if (limit <= 0)
        {
            return new FastbootResponse { Result = FastbootState.Fail, Response = "invalid sparse limit" };
        }

        List<SparseFile> parts = sparseFile.Resparse(limit);
        if (parts.Count == 0)
        {
            return new FastbootResponse { Result = FastbootState.Fail, Response = "sparse resparse returned no parts" };
        }

        FastbootResponse last = new FastbootResponse { Result = FastbootState.Success };
        for (int i = 0; i < parts.Count; i++)
        {
            var current = parts[i];
            long sparseLength = current.GetLength(sparse: true, includeCrc: false);

            NotifyCurrentStep($"Sending sparse image {i + 1}/{parts.Count} to {partition}...");
            using var sparseStream = new SparseImageStream(current, 0, current.Header.TotalBlocks, includeCrc: false, fullRange: true, disposeSource: false);

            var download = DownloadData(sparseStream, sparseLength);
            if (download.Result != FastbootState.Success)
            {
                return download;
            }

            NotifyCurrentStep($"Flashing {partition} ({i + 1}/{parts.Count})...");
            last = RawCommand("flash:" + partition);
            if (last.Result != FastbootState.Success)
            {
                return last;
            }
        }

        return last;
    }


}






