namespace FirmwareKit.Comm.Fastboot;

using FirmwareKit.Sparse.Core;
using FirmwareKit.Sparse.Models;

public partial class FastbootDriver
{
    /// <summary>
    /// Flashes an image stream. If image is sparse or exceeds max-download-size, it is sent as sparse chunks.
    /// <para>刷写镜像流。如果镜像是稀疏的或超过最大下载大小，则以稀疏块形式发送。</para>
    /// </summary>
    /// <param name="partition">The partition to flash. <para>要刷写的分区。</para></param>
    /// <param name="stream">The image stream to flash. <para>要刷写的镜像流。</para></param>
    /// <param name="imageSize">The size of the image in bytes. <para>镜像大小（字节）。</para></param>
    /// <returns>A FastbootResponse indicating the result of the operation. <para>指示操作结果的 FastbootResponse。</para></returns>
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
        FastbootDebug.Log($"FlashUnsparseImage: imageSize={imageSize}, maxDownloadSize={maxDownloadSize}, isSparse={isSparse}, ConvertSimgToRaw={ConvertSimgToRaw}");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool success = false;
        try
        {
            if (isSparse && ConvertSimgToRaw)
            {
                NotifyCurrentStep($"Converting sparse image to raw image for {partition}...");
                stream.Seek(originalPosition, SeekOrigin.Begin);

                using var tempSparseImage = new TempFile("fastboot_sparse_", ".img");
                using var tempRawImage = new TempFile("fastboot_raw_", ".img");

                using (var sparseFileStream = tempSparseImage.OpenWrite())
                {
                    stream.CopyTo(sparseFileStream);
                }

                using (var sparseImage = SparseFile.ImportAuto(tempSparseImage.OpenRead(), validateCrc: false, verbose: false))
                using (var rawStream = tempRawImage.OpenWrite())
                using (var sparseStream = new FirmwareKit.Sparse.Streams.SparseStream(sparseImage))
                {
                    sparseStream.CopyTo(rawStream);
                }

                NotifyCurrentStep($"Flashing raw image to {partition}...");
                using (var rawFileStream = tempRawImage.OpenRead())
                {
                    var resp = FlashRawImage(partition, rawFileStream, rawFileStream.Length);
                    success = resp.Result == FastbootState.Success;
                    return resp;
                }
            }
            else if (isSparse)
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

            const int blockSize = 4096;
            long alignedSize = ((imageSize + blockSize - 1) / blockSize) * blockSize;

            using var rawSparseImage = new SparseFile(blockSize, alignedSize);

            var rawData = new byte[alignedSize];
            int totalRead = 0;
            while (totalRead < imageSize)
            {
                int read = stream.Read(rawData, totalRead, (int)(imageSize - totalRead));
                if (read == 0) break;
                totalRead += read;
            }

            if (totalRead > 0)
            {
                rawSparseImage.AddRawChunk(rawData);
            }

            var rawResp = FlashSparseFile(partition, rawSparseImage, maxDownloadSize);
            success = rawResp.Result == FastbootState.Success;
            return rawResp;
        }
        finally
        {
            sw.Stop();
            OnStepFinished?.Invoke($"Flash {partition}", sw.Elapsed, success);
        }
    }


}




