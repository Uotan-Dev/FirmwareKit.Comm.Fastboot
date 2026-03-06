

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Flashes raw image (corresponding to AOSP flash:raw)
    /// </summary>
    public FastbootResponse FlashRaw(string partition, string kernelPath, string? ramdiskPath = null, string? secondPath = null, string? dtbPath = null, string? cmdline = null, uint header_version = 0, uint base_addr = 0x10000000, uint page_size = 2048)
    {
        FastbootDebug.Log($"FlashRaw(partition={partition}, kernelPath={kernelPath}, ramdiskPath={ramdiskPath}, secondPath={secondPath}, dtbPath={dtbPath}, cmdline={cmdline}, header_version={header_version}, base_addr={base_addr}, page_size={page_size})");
        byte[] kernel = File.ReadAllBytes(kernelPath);
        byte[]? ramdisk = ramdiskPath != null ? File.ReadAllBytes(ramdiskPath) : null;
        byte[]? second = secondPath != null ? File.ReadAllBytes(secondPath) : null;
        byte[]? dtb = dtbPath != null ? File.ReadAllBytes(dtbPath) : null;

        byte[] bootImg = CreateBootImageVersioned(kernel, ramdisk, second, dtb, cmdline, null, header_version, base_addr, page_size);
        using var ms = new MemoryStream(bootImg);
        return FlashUnsparseImage(partition, ms, ms.Length);
    }


}






