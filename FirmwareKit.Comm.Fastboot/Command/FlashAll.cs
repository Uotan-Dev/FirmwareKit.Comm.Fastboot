
using System.IO.Compression;


namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
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

        string slotSuffix = "";
        try { slotSuffix = GetVar("slot-suffix"); } catch { }

        string[] firmwareImages = { "bootloader", "radio" };
        foreach (var p in firmwareImages)
        {
            string img = Path.Combine(androidProductOut, p + ".img");
            if (File.Exists(img))
            {
                NotifyCurrentStep($"Flashing {p}...");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool success = false;
                try
                {
                    using var fs = File.OpenRead(img);
                    FlashUnsparseImage(p, fs, fs.Length).ThrowIfError();
                    success = true;
                }
                finally
                {
                    sw.Stop();
                    OnStepFinished?.Invoke($"Flashing {p}", sw.Elapsed, success);
                }
            }
        }

        string superEmpty = Path.Combine(androidProductOut, "super_empty.img");
        if (File.Exists(superEmpty))
        {
            FlashDynamicPartitions(androidProductOut, superEmpty);
        }

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
                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool success = false;
                try
                {
                    using var fs = File.OpenRead(img);
                    FlashUnsparseImage(p, fs, fs.Length).ThrowIfError();
                    success = true;
                }
                finally
                {
                    sw.Stop();
                    OnStepFinished?.Invoke($"Flashing {p}", sw.Elapsed, success);
                }
            }
        }

        NotifyCurrentStep("Flash completed.");
        OnStepFinished?.Invoke("Flash completed", TimeSpan.Zero, true);
    }

    private void FlashDynamicPartitions(string directory, string superEmptyPath)
    {
        NotifyCurrentStep("Flashing dynamic partitions...");

        string[] dynamicPartitions = { "system", "vendor", "product", "system_ext", "odm", "vendor_dlkm", "odm_dlkm" };
        bool hasAnyImage = false;
        foreach (var p in dynamicPartitions)
        {
            if (File.Exists(Path.Combine(directory, p + ".img")))
            {
                hasAnyImage = true;
                break;
            }
        }

        if (!hasAnyImage) return;

        try
        {
            var helper = new SuperFlashHelper(this, "super", superEmptyPath);
            foreach (var p in dynamicPartitions)
            {
                string img = Path.Combine(directory, p + ".img");
                if (File.Exists(img))
                {
                    helper.AddPartition(p, img);
                }
            }
            helper.Flash();
        }
        catch (Exception ex)
        {
            NotifyCurrentStep($"Warning: Optimized super flash failed ({ex.Message}). Falling back to individual partition flashing...");
            // Individual fallback
            foreach (var p in dynamicPartitions)
            {
                string img = Path.Combine(directory, p + ".img");
                if (File.Exists(img))
                {
                    string target = p + (GetVar("slot-suffix") ?? "");
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    bool success = false;
                    try
                    {
                        using var fs = File.OpenRead(img);
                        FlashUnsparseImage(target, fs, fs.Length).ThrowIfError();
                        success = true;
                    }
                    finally
                    {
                        sw.Stop();
                        OnStepFinished?.Invoke($"Flashing {target}", sw.Elapsed, success);
                    }
                }
            }
        }
    }
}




