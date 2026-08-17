using FirmwareKit.Lp;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Wipes the super partition. With no empty image path, falls back to the raw
    /// <c>wipe-super:&lt;partition&gt;</c> protocol command; with an empty image path, follows the
    /// AOSP <c>wipe_super()</c> flow (parse super_empty metadata, generate an empty super image and
    /// flash it per block device).
    /// <para>清除 super 分区。未提供空镜像路径时回退到原始 <c>wipe-super:&lt;partition&gt;</c> 协议命令；
    /// 提供空镜像路径时遵循 AOSP <c>wipe_super()</c> 流程（解析 super_empty 元数据，生成空 super 镜像并
    /// 按块设备逐一刷写）。</para>
    /// </summary>
    /// <param name="partition">Target super partition (e.g. "super"). <para>目标 super 分区（如 "super"）。</para></param>
    /// <param name="emptyImagePath">Optional path to super_empty.img. <para>可选的 super_empty.img 路径。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse WipeSuper(string partition, string? emptyImagePath = null)
    {
        if (string.IsNullOrWhiteSpace(emptyImagePath) || !File.Exists(emptyImagePath))
        {
            // Fallback for bootloaders that support the device-side wipe-super command.
            return RawCommand("wipe-super:" + partition);
        }

        LpMetadata metadata;
        try
        {
            metadata = ReadFromImageFile(emptyImagePath!);
        }
        catch (Exception ex)
        {
            return new FastbootResponse
            {
                Result = FastbootState.Fail,
                Response = "failed to parse super_empty image: " + ex.Message
            };
        }

        if (metadata.BlockDevices.Count == 0)
        {
            return new FastbootResponse
            {
                Result = FastbootState.Fail,
                Response = "super_empty image contains no block devices"
            };
        }

        // AOSP wipe_super(): retrofit devices (super block device named other than "super")
        // reject flashing until "oem allow-flash-super" is sent.
        string superBdevName = metadata.BlockDevices[0].GetPartitionName();
        if (!string.Equals(superBdevName, "super", StringComparison.OrdinalIgnoreCase))
        {
            var oem = OemCommand("allow-flash-super");
            if (oem.Result != FastbootState.Success) return oem;
        }

        long maxDownloadSize = GetMaxDownloadSize();
        for (int i = 0; i < metadata.BlockDevices.Count; i++)
        {
            var bdev = metadata.BlockDevices[i];
            string bdevPartition = bdev.GetPartitionName();
            bool forceSlot = (bdev.Flags & MetadataFormat.LP_BLOCK_DEVICE_SLOT_SUFFIXED) != 0;

            // AOSP do_for_partitions(): slot-suffixed block devices flash the current slot.
            string target = bdevPartition;
            if (forceSlot && HasSlot(bdevPartition))
            {
                string current = GetCurrentSlot();
                if (!string.IsNullOrEmpty(current)) target = bdevPartition + "_" + current;
            }

            // Empty super image: geometry + metadata slots + backup, no partition payload.
            var builder = new SuperImageBuilder(MetadataBuilder.FromMetadata(metadata));
            using var sparse = builder.Build((uint)i);
            var flash = FlashSparseFile(target, sparse, maxDownloadSize);
            if (flash.Result != FastbootState.Success) return flash;
        }

        return new FastbootResponse { Result = FastbootState.Success };
    }
}
