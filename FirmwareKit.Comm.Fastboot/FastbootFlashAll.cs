using System.IO.Compression;

namespace FirmwareKit.Comm.Fastboot;

public class FastbootFlashAll(FastbootUtil util)
{
    private readonly FastbootUtil _util = util;
    private readonly ProductInfoParser _parser = new(util);

    public void FlashUpdateZip(string zipPath, bool skipSecondary = false)
    {
        _util.NotifyCurrentStep("Extracting update zip...");
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

    public void FlashFromDirectory(string directory, bool skipSecondary = false)
    {
        string androidProductOut = directory;

        // 1. Check android-info.txt
        string infoPath = Path.Combine(androidProductOut, "android-info.txt");
        if (File.Exists(infoPath))
        {
            _util.NotifyCurrentStep("Verifying device compatibility...");
            string content = File.ReadAllText(infoPath);
            if (!_parser.Validate(content, out string? error))
            {
                throw new Exception("Incompatible device: " + error);
            }
        }

        // 2. Determine partitions to flash
        // Standard AOSP behavior: flash partitions in a specific order
        // and handle logical partitions via super_empty.img if present.

        var partitions = GetPartitionList(androidProductOut);

        // 3. Handle Super Partition Optimization (dynamic partitions)
        string superEmpty = Path.Combine(androidProductOut, "super_empty.img");
        if (File.Exists(superEmpty))
        {
            FlashDynamicPartitions(androidProductOut, superEmpty);
        }

        // 4. Flash normal partitions
        foreach (var (part, file) in partitions)
        {
            if (IsDynamicPartition(part)) continue; // Already handled

            _util.NotifyCurrentStep($"Flashing {part}...");
            FlashImage(part, file);
        }

        _util.NotifyCurrentStep("Flash completed.");
    }

    private void FlashDynamicPartitions(string directory, string superEmptyPath)
    {
        _util.NotifyCurrentStep("Flashing dynamic partitions...");
        var helper = new SuperFlashHelper(_util, "super", superEmptyPath);

        // Find all images that should be in super
        // Usually these are system, vendor, product, system_ext, odm, etc.
        string[] dynamicPartitions = ["system", "vendor", "product", "system_ext", "odm", "vendor_dlkm", "odm_dlkm"];

        bool addedAny = false;
        foreach (var p in dynamicPartitions)
        {
            string img = Path.Combine(directory, p + ".img");
            if (File.Exists(img))
            {
                helper.AddPartition(p, img);
                addedAny = true;
            }

            // Handle A/B slots for dynamic partitions if needed
            string imgA = Path.Combine(directory, p + "_a.img");
            if (File.Exists(imgA))
            {
                helper.AddPartition(p + "_a", imgA);
                addedAny = true;
            }
        }

        if (addedAny)
        {
            helper.Flash();
        }
    }

    private void FlashImage(string partition, string filePath)
    {
        // Check if sparse
        if (IsSparseImage(filePath))
        {
            _util.FlashSparseImage(partition, filePath).ThrowIfError();
        }
        else
        {
            using var fs = File.OpenRead(filePath);
            _util.FlashUnsparseImage(partition, fs, fs.Length).ThrowIfError();
        }
    }

    private bool IsSparseImage(string path)
    {
        using var fs = File.OpenRead(path);
        byte[] header = new byte[4];
        if (fs.Read(header, 0, 4) == 4)
        {
            return BitConverter.ToUInt32(header, 0) == 0xED26FF3A;
        }
        return false;
    }

    private bool IsDynamicPartition(string name)
    {
        // Simple heuristic: if it's in the list of typical dynamic partitions
        string[] dynamic = ["system", "vendor", "product", "system_ext", "odm", "vendor_dlkm", "odm_dlkm"];
        return dynamic.Any(d => name == d || name.StartsWith(d + "_"));
    }

    private List<(string partition, string file)> GetPartitionList(string directory)
    {
        var list = new List<(string, string)>();
        string[] priority = ["boot", "init_boot", "vendor_boot", "dtbo", "vbmeta", "vbmeta_system", "vbmeta_vendor", "recovery"];

        foreach (var p in priority)
        {
            string path = Path.Combine(directory, p + ".img");
            if (File.Exists(path)) list.Add((p, path));
        }

        // Add others found in directory
        foreach (var file in Directory.GetFiles(directory, "*.img"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!priority.Contains(name) && name != "super_empty")
            {
                list.Add((name, file));
            }
        }

        return list;
    }
}
