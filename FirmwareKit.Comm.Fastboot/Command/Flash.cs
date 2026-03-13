namespace FirmwareKit.Comm.Fastboot;

using FirmwareKit.Sparse.Core;
using FirmwareKit.Sparse.Models;

public partial class FastbootDriver
{
    /// <summary>
    /// Flashes an image stream. If image is sparse or exceeds max-download-size, it is sent as sparse chunks.
    /// </summary>
    public FastbootResponse FlashUnsparseImage(string partition, Stream stream, long imageSize)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (string.IsNullOrWhiteSpace(partition)) throw new ArgumentException("partition is required", nameof(partition));
        if (imageSize <= 0)
        {
            return new FastbootResponse { Result = FastbootState.Fail, Response = "invalid image size" };
        }

        bool canSeek = stream.CanSeek;
        long originalPosition = canSeek ? stream.Position : 0;
        long maxDownloadSize = GetMaxDownloadSize();

        static bool IsSparseHeader(Stream s)
        {
            byte[] header = new byte[4];
            int read = s.Read(header, 0, 4);
            return read == 4 && BitConverter.ToUInt32(header, 0) == SparseFormat.SparseHeaderMagic;
        }

        bool isSparse = false;
        if (canSeek)
        {
            stream.Seek(originalPosition, SeekOrigin.Begin);
            isSparse = IsSparseHeader(stream);
            stream.Seek(originalPosition, SeekOrigin.Begin);
        }

        if (IsLogicalOptimized(partition))
        {
            // Match AOSP behavior: logical partitions are resized to image logical size before flashing.
            ResizeLogicalPartition(partition, imageSize);
        }

        // log sizes so that callers can understand why we convert to sparse
        FastbootDebug.Log($"FlashUnsparseImage: imageSize={imageSize}, maxDownloadSize={maxDownloadSize}, isSparse={isSparse}");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool success = false;
        try
        {
            if (isSparse)
            {
                NotifyCurrentStep($"Flashing sparse image to {partition}...");
                using var sparseImage = SparseFile.ImportAuto(stream, validateCrc: false, verbose: false);
                var resp = FlashSparseFile(partition, sparseImage, maxDownloadSize);
                success = resp.Result == FastbootState.Success;
                return resp;
            }

            // Only send raw if the image will fit in a single transfer
            if (imageSize <= maxDownloadSize)
            {
                if (canSeek) stream.Seek(originalPosition, SeekOrigin.Begin);
                var resp = FlashRawImage(partition, stream, imageSize);
                success = resp.Result == FastbootState.Success;
                return resp;
            }

            if (!canSeek)
            {
                return new FastbootResponse
                {
                    Result = FastbootState.Fail,
                    Response = "raw image exceeds max-download-size and requires a seekable stream for sparse conversion"
                };
            }

            NotifyCurrentStep($"Converting large raw image to sparse chunks for {partition}...");
            stream.Seek(originalPosition, SeekOrigin.Begin);
            using (var sparseImage = SparseFile.ImportAuto(stream, validateCrc: false, verbose: false))
            {
                var resp = FlashSparseFile(partition, sparseImage, maxDownloadSize);
                success = resp.Result == FastbootState.Success;
                return resp;
            }
        }
        finally
        {
            sw.Stop();
            OnStepFinished?.Invoke($"Flash {partition}", sw.Elapsed, success);
        }
    }


}




