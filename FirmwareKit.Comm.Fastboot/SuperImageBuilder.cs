using FirmwareKit.Lp;
using FirmwareKit.Sparse.Core;

namespace FirmwareKit.Comm.Fastboot;

public class SuperImageBuilder
{
    private readonly MetadataBuilder _builder;
    private readonly Dictionary<string, string> _partitionImages = [];
    private readonly uint _blockSize;

    public SuperImageBuilder(ulong deviceSize, uint metadataMaxSize, uint metadataSlotCount, uint blockSize = 4096)
    {
        _builder = MetadataBuilder.New(deviceSize, metadataMaxSize, metadataSlotCount);
        _blockSize = blockSize;
    }

    public SuperImageBuilder(MetadataBuilder builder, uint blockSize = 4096)
    {
        _builder = builder;
        _blockSize = blockSize;
    }

    public Partition? FindPartition(string name) => _builder.FindPartition(name);

    public void AddPartition(string name, ulong size, string groupName, uint attributes, string? imagePath = null)
    {
        _builder.AddPartition(name, groupName, attributes);
        UpdatePartitionImage(name, size, imagePath);
    }

    public void UpdatePartitionImage(string name, ulong size, string? imagePath = null)
    {
        var partition = _builder.FindPartition(name);
        if (partition != null)
        {
            _builder.ResizePartition(partition, size);
        }

        if (!string.IsNullOrEmpty(imagePath))
        {
            _partitionImages[name] = imagePath!;
        }
    }

    public void AddGroup(string name, ulong maxSize) => _builder.AddGroup(name, maxSize);

    public SparseFile Build()
    {
        var metadata = _builder.Export();
        var writer = new MetadataWriter();
        var geometryBlob = writer.SerializeGeometry(metadata.Geometry);
        var metadataBlob = writer.SerializeMetadata(metadata);

        var sparseFile = new SparseFile(_blockSize, (long)metadata.BlockDevices[0].Size);

        sparseFile.AddDontCareChunk(MetadataFormat.LP_PARTITION_RESERVED_BYTES);
        sparseFile.AddRawChunk(geometryBlob);
        sparseFile.AddRawChunk(geometryBlob);

        for (var i = 0; i < metadata.Geometry.MetadataSlotCount; i++)
        {
            var slotData = new byte[metadata.Geometry.MetadataMaxSize];
            Array.Copy(metadataBlob, slotData, Math.Min(metadataBlob.Length, slotData.Length));
            sparseFile.AddRawChunk(slotData);
        }

        var metadataEndOffset = MetadataFormat.LP_PARTITION_RESERVED_BYTES +
                                 ((long)geometryBlob.Length * 2) +
                                 ((long)metadata.Geometry.MetadataMaxSize * metadata.Geometry.MetadataSlotCount);

        var firstLogicalOffset = (long)metadata.BlockDevices[0].FirstLogicalSector * MetadataFormat.LP_SECTOR_SIZE;

        if (firstLogicalOffset > metadataEndOffset)
        {
            sparseFile.AddDontCareChunk((uint)(firstLogicalOffset - metadataEndOffset));
        }

        var allExtents = new List<(string PartitionName, LpMetadataExtent Extent)>();
        foreach (var p in metadata.Partitions)
        {
            var name = p.GetName();
            for (var i = 0; i < p.NumExtents; i++)
            {
                allExtents.Add((name, metadata.Extents[(int)(p.FirstExtentIndex + i)]));
            }
        }

        var sortedExtents = allExtents
            .Where(e => e.Extent.TargetType == MetadataFormat.LP_TARGET_TYPE_LINEAR)
            .OrderBy(e => e.Extent.TargetData)
            .ToList();

        var currentLogicalOffset = firstLogicalOffset;

        for (var i = 0; i < sortedExtents.Count; i++)
        {
            var (partitionName, extent) = sortedExtents[i];
            var extentOffset = (long)extent.TargetData * MetadataFormat.LP_SECTOR_SIZE;
            var extentSize = (long)extent.NumSectors * MetadataFormat.LP_SECTOR_SIZE;

            if (extentOffset > currentLogicalOffset)
            {
                sparseFile.AddDontCareChunk((uint)(extentOffset - currentLogicalOffset));
            }

            if (_partitionImages.TryGetValue(partitionName, out var imagePath))
            {
                long writtenForThisPartition = 0;
                for (var j = 0; j < i; j++)
                {
                    var (pName, pExtent) = sortedExtents[j];
                    if (pName == partitionName)
                    {
                        writtenForThisPartition += (long)pExtent.NumSectors * MetadataFormat.LP_SECTOR_SIZE;
                    }
                }

                var imgInfo = new FileInfo(imagePath);
                var toWrite = Math.Min(extentSize, imgInfo.Length - writtenForThisPartition);

                if (toWrite > 0)
                {
                    sparseFile.AddRawFileChunk(imagePath, writtenForThisPartition, (uint)toWrite);
                    if (toWrite < extentSize)
                    {
                        sparseFile.AddFillChunk(0, (uint)(extentSize - toWrite));
                    }
                }
                else
                {
                    sparseFile.AddFillChunk(0, (uint)extentSize);
                }
            }
            else
            {
                sparseFile.AddFillChunk(0, (uint)extentSize);
            }

            currentLogicalOffset = extentOffset + extentSize;
        }

        var totalDeviceSize = (long)metadata.BlockDevices[0].Size;
        var backupMetadataSize = (long)metadata.Geometry.MetadataMaxSize * metadata.Geometry.MetadataSlotCount;
        var backupMetadataStart = totalDeviceSize - backupMetadataSize;

        if (backupMetadataStart > currentLogicalOffset)
        {
            sparseFile.AddDontCareChunk((uint)(backupMetadataStart - currentLogicalOffset));
        }

        for (var i = 0; i < metadata.Geometry.MetadataSlotCount; i++)
        {
            var slotData = new byte[metadata.Geometry.MetadataMaxSize];
            Array.Copy(metadataBlob, slotData, Math.Min(metadataBlob.Length, slotData.Length));
            sparseFile.AddRawChunk(slotData);
        }

        return sparseFile;
    }


}