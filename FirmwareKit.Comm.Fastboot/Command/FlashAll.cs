using FirmwareKit.Comm.Fastboot.Utils;
using System.IO.Compression;


namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    private ProductInfoParser _productParser => new(this);

    /// <summary>
    /// Performs a "flashall" operation from a standard AOSP update.zip
    /// </summary>
    public void FlashUpdateZip(string zipPath, bool skipSecondary = false)
    {
        NotifyCurrentStep("Extracting update zip...");
        string tempDir = Path.Combine(Path.GetTempPath(), "fastboot_update_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, tempDir);
            FlashFromDirectory(tempDir, skipSecondary);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Performs a "flashall" operation from a directory containing AOSP images
    /// </summary>
    public void FlashFromDirectory(string directory, bool skipSecondary = false)
    {
        string androidProductOut = directory;

        // 1. Check android-info.txt
        string infoPath = Path.Combine(androidProductOut, "android-info.txt");
        if (File.Exists(infoPath))
        {
            NotifyCurrentStep("Verifying device compatibility...");
            string content = File.ReadAllText(infoPath);
            if (!_productParser.Validate(content, out string? error))
            {
                throw new Exception("Incompatible device: " + error);
            }
        }

        // 2. Flash dynamic partitions if super_empty.img exists
        string superEmpty = Path.Combine(androidProductOut, "super_empty.img");
        if (File.Exists(superEmpty))
        {
            FlashDynamicPartitions(androidProductOut, superEmpty);
        }

        // 3. Flash other common partitions
        string[] standardImages = {
            "boot", "init_boot", "vendor_boot", "dtbo", "pvmfw",
            "vbmeta", "vbmeta_system", "vbmeta_vendor", "recovery",
            "vendor_kernel_boot"
        };

        foreach (var p in standardImages)
        {
            string img = Path.Combine(androidProductOut, p + ".img");
            if (File.Exists(img))
            {
                NotifyCurrentStep($"Flashing {p}...");
                using var fs = File.OpenRead(img);
                FlashUnsparseImage(p, fs, fs.Length).ThrowIfError();
            }
        }

        NotifyCurrentStep("Flash completed.");
    }

    private void FlashDynamicPartitions(string directory, string superEmptyPath)
    {
        NotifyCurrentStep("Flashing dynamic partitions...");
        var helper = new SuperFlashHelper(this, "super", superEmptyPath);

        string[] dynamicPartitions = { "system", "vendor", "product", "system_ext", "odm", "vendor_dlkm", "odm_dlkm" };

        bool addedAny = false;
        foreach (var p in dynamicPartitions)
        {
            string img = Path.Combine(directory, p + ".img");
            if (File.Exists(img))
            {
                helper.AddPartition(p, img);
                addedAny = true;
            }
        }

        if (addedAny)
        {
            helper.Flash();
        }
    }
}




