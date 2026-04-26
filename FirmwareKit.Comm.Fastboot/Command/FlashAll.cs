
using System.IO.Compression;


namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    private ProductInfoParser _productParser => new(this);

    /// <summary>
    /// Performs a "flashall" operation from a standard AOSP update.zip
    /// <para>从标准 AOSP update.zip 执行 "flashall" 操作。</para>
    /// </summary>
    /// <param name="zipPath">Path to the update.zip file. <para>update.zip 文件的路径。</para></param>
    /// <param name="skipSecondary">Whether to skip flashing to the secondary slot. <para>是否跳过刷写到次要槽位。</para></param>
    /// <param name="disableVerity">Whether to disable dm-verity. <para>是否禁用 dm-verity。</para></param>
    /// <param name="disableVerification">Whether to disable verification. <para>是否禁用验证。</para></param>
    /// <param name="force">Whether to force flashing even if requirements are not met. <para>即使不满足要求也强制刷写。</para></param>
    /// <param name="optimizeSuper">Whether to optimize super partition flashing. <para>是否优化 super 分区刷写。</para></param>
    /// <param name="disableFastbootInfo">Whether to disable use of fastboot-info.txt. <para>是否禁用使用 fastboot-info.txt。</para></param>
    /// <param name="excludeDynamicPartitions">Whether to exclude dynamic partitions. <para>是否排除动态分区。</para></param>
    public void FlashUpdateZip(string zipPath, bool skipSecondary = false, bool disableVerity = false, bool disableVerification = false, bool force = false, bool optimizeSuper = true, bool disableFastbootInfo = false, bool excludeDynamicPartitions = false)
    {
        NotifyCurrentStep("Extracting update zip...");
        string tempDir = Path.Combine(Path.GetTempPath(), "fastboot_update_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, tempDir);
            FlashFromDirectory(tempDir, skipSecondary, disableVerity, disableVerification, force, optimizeSuper, disableFastbootInfo, excludeDynamicPartitions);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Performs a "flashall" operation from a directory containing AOSP images
    /// <para>从包含 AOSP 镜像的目录执行 "flashall" 操作。</para>
    /// </summary>
    /// <param name="directory">Path to the directory containing images. <para>包含镜像的目录路径。</para></param>
    /// <param name="skipSecondary">Whether to skip flashing to the secondary slot. <para>是否跳过刷写到次要槽位。</para></param>
    /// <param name="disableVerity">Whether to disable dm-verity. <para>是否禁用 dm-verity。</para></param>
    /// <param name="disableVerification">Whether to disable verification. <para>是否禁用验证。</para></param>
    /// <param name="force">Whether to force flashing even if requirements are not met. <para>即使不满足要求也强制刷写。</para></param>
    /// <param name="optimizeSuper">Whether to optimize super partition flashing. <para>是否优化 super 分区刷写。</para></param>
    /// <param name="disableFastbootInfo">Whether to disable use of fastboot-info.txt. <para>是否禁用使用 fastboot-info.txt。</para></param>
    /// <param name="excludeDynamicPartitions">Whether to exclude dynamic partitions. <para>是否排除动态分区。</para></param>
    public void FlashFromDirectory(string directory, bool skipSecondary = false, bool disableVerity = false, bool disableVerification = false, bool force = false, bool optimizeSuper = true, bool disableFastbootInfo = false, bool excludeDynamicPartitions = false)
    {
        FlashAll(directory, false, skipSecondary, force, optimizeSuper, disableVerity, disableVerification, disableFastbootInfo, excludeDynamicPartitions);
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




