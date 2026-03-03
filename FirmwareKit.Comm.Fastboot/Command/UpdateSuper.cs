using System;
using System.IO;
using FirmwareKit.Comm.Fastboot.DataModel;
using FirmwareKit.Lp;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Updates Super partition metadata (corresponding to update-super)
        /// </summary>
        public FastbootResponse UpdateSuper(string partition, string metadataPath, bool wipe = false)
        {
            if (!File.Exists(metadataPath)) throw new FileNotFoundException(metadataPath);

            EnsureUserspace();

            var metadataReader = new MetadataReader();
            var metadataWriter = new MetadataWriter();
            LpMetadata metadata = metadataReader.ReadFromImageFile(metadataPath);
            byte[] metadataBlob = metadataWriter.SerializeMetadata(metadata);

            NotifyCurrentStep($"Updating super metadata for {partition}");
            DownloadData(metadataBlob).ThrowIfError();

            string command = "update-super:" + partition;
            if (wipe)
            {
                command += ":wipe";
            }
            return RawCommand(command);
        }
    }
}
