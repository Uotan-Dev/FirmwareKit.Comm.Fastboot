using FirmwareKit.Lp;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Updates the super partition metadata from a metadata image file.
    /// <para>从元数据镜像文件更新 super 分区元数据。</para>
    /// </summary>
    public FastbootResponse UpdateSuper(string partition, string metadataPath, bool wipe = false)
    {
        if (!File.Exists(metadataPath)) throw new FileNotFoundException(metadataPath);

        EnsureUserspace();

        var metadataReader = new MetadataReader();
        var metadataWriter = new MetadataWriter();
        LpMetadata metadata;
        try
        {
            metadata = metadataReader.ReadFromImageFile(metadataPath);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Failed to parse super metadata image: {metadataPath}", ex);
        }
        byte[] metadataBlob = metadataWriter.SerializeMetadata(metadata);

        NotifyCurrentStep($"Updating super metadata for {partition}");
        DownloadData(metadataBlob).ThrowIfError();

        string command = wipe ? $"update-super:{partition}:wipe" : $"update-super:{partition}";
        return RawCommand(command);
    }
}
