using FirmwareKit.Lp;
using FirmwareKit.Sparse.Core;

namespace FirmwareKit.Comm.Fastboot;

public class SuperFlashHelper(FastbootUtil fastboot, string mainPartition = "super", string? emptyImagePath = null)
{
    private FastbootUtil _fastboot = fastboot;
    private SuperImageBuilder _builder = InitializeBuilder(fastboot, mainPartition, emptyImagePath);
    private string _mainPartition = mainPartition;

    private static SuperImageBuilder InitializeBuilder(FastbootUtil fastboot, string mainPartition, string? emptyImagePath)
    {
        ulong superSize = 0;

        if (!string.IsNullOrEmpty(emptyImagePath) && File.Exists(emptyImagePath))
        {
            try
            {
                var metadataReader = new MetadataReader();
                var metadata = metadataReader.ReadFromImageFile(emptyImagePath);
                var builder = MetadataBuilder.FromMetadata(metadata);
                return new SuperImageBuilder(builder);
            }
            catch
            {
                return CreateDefaultBuilder(fastboot, mainPartition, ref superSize);
            }
        }
        else
        {
            return CreateDefaultBuilder(fastboot, mainPartition, ref superSize);
        }
    }

    private static SuperImageBuilder CreateDefaultBuilder(FastbootUtil fastboot, string mainPartition, ref ulong superSize)
    {
        string sizeStr = fastboot.GetPartitionSize(mainPartition);
        if (!string.IsNullOrEmpty(sizeStr))
        {
            if (sizeStr.StartsWith("0x")) superSize = Convert.ToUInt64(sizeStr.Substring(2), 16);
            else superSize = Convert.ToUInt64(sizeStr);
        }

        if (superSize == 0) superSize = 1024L * 1024 * 1024 * 4;

        var builder = new SuperImageBuilder(superSize, 65536, 2);
        builder.AddGroup("default", superSize);
        return builder;
    }

    public void AddPartition(string name, string imagePath, string groupName = "default")
    {
        var info = new FileInfo(imagePath);
        var partition = _builder.FindPartition(name);
        if (partition == null)
        {
            _builder.AddPartition(name, (ulong)info.Length, groupName, MetadataFormat.LP_PARTITION_ATTR_READONLY, imagePath);
        }
        else
        {
            _builder.UpdatePartitionImage(name, (ulong)info.Length, imagePath);
        }
    }

    public void Flash()
    {
        _fastboot.NotifyCurrentStep($"Building optimized {_mainPartition} image (streaming)...");
        using (SparseFile superSparse = _builder.Build())
        {
            long maxDownloadSize = _fastboot.GetMaxDownloadSize();
            _fastboot.FlashSparseFile(_mainPartition, superSparse, maxDownloadSize);
        }
    }


}