
using FirmwareKit.Sparse.Core;


namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Flashes non-sparse image, automatically handling sparse split if larger than max-download-size
    /// </summary>
    public FastbootResponse FlashUnsparseImage(string partition, Stream stream, long length)
    {
        FastbootDebug.Log($"FlashUnsparseImage(partition={partition}, length={length})");
        if (IsLogicalOptimized(partition) && !IsUserspace())
        {
            throw new Exception($"Partition {partition} is a logical partition, which requires fastbootd to flash.");
        }

        long maxDownloadSize = GetMaxDownloadSize();
        FastbootDebug.Log($"Max Download Size: {maxDownloadSize}");

        // Check if stream IS already a sparse file by peek magic
        byte[] magic = new byte[4];
        long originalPos = stream.Position;
        int magicRead = stream.Read(magic, 0, 4);
        stream.Position = originalPos;

        bool isSparse = magicRead == 4 && magic[0] == 0x3a && magic[1] == 0xff && magic[2] == 0x26 && magic[3] == 0xed;
        FastbootDebug.Log($"isSparse: {isSparse}, magicRead: {magicRead}, magic: {(magicRead == 4 ? BitConverter.ToString(magic) : "N/A")}");

        if (isSparse)
        {
            try
            {
                NotifyCurrentStep($"{partition} is a sparse image. Processing...");
                using var sfile = SparseFile.FromStream(stream);
                FastbootDebug.Log("SparseFile.FromStream success.");
                return FlashSparseFile(partition, sfile, maxDownloadSize);
            }
            catch (Exception ex)
            {
                FastbootDebug.Log($"SparseFile.FromStream FAILED: {ex.Message}");
                NotifyCurrentStep($"Warning: Failed to parse sparse image ({ex.Message}). Falling back to RAW flashing...");
                stream.Position = originalPos; // Ensure we start from the beginning for RAW flash
                // Continue to RAW flashing logic below
            }
        }

        if (length > maxDownloadSize)
        {
            FastbootDebug.Log($"Large image detected, splitting RAW: {length} > {maxDownloadSize}");
            NotifyCurrentStep($"{partition} ({length} bytes) is larger than max-download-size ({maxDownloadSize}). Splitting RAW image...");
            // Manually split RAW stream into sparse-like chunks for download
            long bytesWritten = 0;
            int count = 1;
            long totalChunks = (length + maxDownloadSize - 1) / maxDownloadSize;

            while (bytesWritten < length)
            {
                long toWrite = Math.Min(maxDownloadSize, length - bytesWritten);
                NotifyCurrentStep($"Sending {partition} RAW chunk {count}/{totalChunks} ({toWrite} bytes)");

                // Create a sub-view of the RAW stream
                using var subStream = new SubStream(stream, bytesWritten, toWrite);
                DownloadData(subStream, toWrite).ThrowIfError();

                NotifyCurrentStep($"Flashing {partition} RAW chunk {count}/{totalChunks}");
                RawCommand("flash:" + partition).ThrowIfError();

                bytesWritten += toWrite;
                count++;
            }
            return new FastbootResponse { Result = FastbootState.Success };
        }

        // AVB Footer logic
        if (!IsLogicalOptimized(partition))
        {
            long partitionSize = GetPartitionSizeLong(partition);
            if (partitionSize > length && length >= FirmwareKit.AVB.AvbFooter.Size)
            {
                stream.Seek(length - FirmwareKit.AVB.AvbFooter.Size, SeekOrigin.Begin);
                byte[] footerBytes = new byte[FirmwareKit.AVB.AvbFooter.Size];
                int read = stream.Read(footerBytes, 0, (int)FirmwareKit.AVB.AvbFooter.Size);

                if (read == (int)FirmwareKit.AVB.AvbFooter.Size)
                {
                    var footer = FirmwareKit.AVB.AvbFooter.FromBytes(footerBytes);

                    if (footer.IsValid)
                    {
                        NotifyCurrentStep($"AVB Footer detected. Patching image to match partition size {partitionSize}...");

                        // Use Advanced Streams to avoid OOM for large partition paddings
                        var imagePart = new SubStream(stream, 0, length - FirmwareKit.AVB.AvbFooter.Size);
                        var padding = new PaddingStream(partitionSize - length);
                        var footerPart = new MemoryStream(footerBytes);

                        using var composite = new ConcatenatedStream(imagePart, padding, footerPart);

                        NotifyCurrentStep($"Sending {partition} (patched to {partitionSize} bytes)");
                        DownloadData(composite, partitionSize).ThrowIfError();
                        NotifyCurrentStep($"Flashing {partition}");
                        return RawCommand("flash:" + partition).ThrowIfError();
                    }
                }
                stream.Seek(0, SeekOrigin.Begin);
            }
        }

        NotifyCurrentStep($"Sending {partition} ({length} bytes)");
        DownloadData(stream, length).ThrowIfError();
        NotifyCurrentStep($"Flashing {partition}");
        return RawCommand("flash:" + partition).ThrowIfError();
    }
}






