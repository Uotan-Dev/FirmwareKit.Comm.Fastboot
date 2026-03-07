
using FirmwareKit.Sparse.Core;
using FirmwareKit.Sparse.Utils;
using System.Buffers;


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
            NotifyCurrentStep($"{partition} ({length} bytes) exceeds max-download-size ({maxDownloadSize}). Converting RAW image to sparse...");
            string tempDir = Path.Combine(Path.GetTempPath(), "fastboot_sparse_" + Guid.NewGuid().ToString("N"));
            string sparsePath = Path.Combine(tempDir, "input.sparse.img");

            try
            {
                Directory.CreateDirectory(tempDir);

                string? sourceRawPath = null;
                bool canUseFileDirectly = stream is FileStream fs &&
                                          fs.CanSeek &&
                                          originalPos == 0 &&
                                          length == fs.Length;

                if (canUseFileDirectly)
                {
                    sourceRawPath = ((FileStream)stream).Name;
                    FastbootDebug.Log($"Converting RAW via direct source file: {sourceRawPath}");
                }
                else
                {
                    string rawPath = Path.Combine(tempDir, "input.raw");
                    sourceRawPath = rawPath;
                    if (stream.CanSeek)
                    {
                        stream.Seek(originalPos, SeekOrigin.Begin);
                    }

                    using var ofs = File.Create(rawPath);
                    byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
                    try
                    {
                        long remaining = length;
                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(copyBuffer.Length, remaining);
                            int read = stream.Read(copyBuffer, 0, toRead);
                            if (read <= 0)
                            {
                                return new FastbootResponse
                                {
                                    Result = FastbootState.Fail,
                                    Response = $"failed to read source RAW stream while converting to sparse: {length - remaining}/{length}"
                                };
                            }
                            ofs.Write(copyBuffer, 0, read);
                            remaining -= read;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(copyBuffer);
                    }
                }

                SparseImageConverter.ConvertRawToSparse(sourceRawPath!, sparsePath, 4096);
                NotifyCurrentStep($"Converted RAW image to sparse. Flashing {partition} with sparse protocol...");
                return FlashSparseImage(partition, sparsePath);
            }
            catch (Exception ex)
            {
                return new FastbootResponse
                {
                    Result = FastbootState.Fail,
                    Response = "raw-to-sparse conversion failed: " + ex.Message
                };
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                if (stream.CanSeek)
                {
                    try { stream.Seek(originalPos, SeekOrigin.Begin); } catch { }
                }
            }
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






