using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootUtil
{
    /// <summary>
    /// Flashes raw image (corresponding to AOSP flash:raw)
    /// </summary>
    public FastbootResponse FlashRaw(string partition, string kernelPath, string? ramdiskPath = null, string? secondPath = null, string? dtbPath = null, string? cmdline = null, uint header_version = 0, uint base_addr = 0x10000000, uint page_size = 2048)
    {
        byte[] kernel = File.ReadAllBytes(kernelPath);
        byte[]? ramdisk = ramdiskPath != null ? File.ReadAllBytes(ramdiskPath) : null;
        byte[]? second = secondPath != null ? File.ReadAllBytes(secondPath) : null;
        byte[]? dtb = dtbPath != null ? File.ReadAllBytes(dtbPath) : null;

        byte[] bootImg = CreateBootImageVersioned(kernel, ramdisk, second, dtb, cmdline, null, header_version, base_addr, page_size);
        DownloadData(bootImg).ThrowIfError();
        return RawCommand("flash:" + partition);
    }


}