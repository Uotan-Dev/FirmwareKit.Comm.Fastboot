namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Boots the device using the specified boot image data.
    /// Downloads the image data and sends the boot command.
    /// <para>使用指定的启动镜像数据启动设备。
    /// 下载镜像数据并发送 boot 命令。</para>
    /// </summary>
    /// <param name="data">Boot image data bytes. <para>启动镜像数据字节。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Boot(byte[] data)
    {
        DownloadData(data).ThrowIfError();
        return RawCommand("boot");
    }

    /// <summary>
    /// Boots the device using a boot image from a stream.
    /// Downloads the image data from stream and sends the boot command.
    /// <para>使用来自流的启动镜像启动设备。
    /// 从流下载镜像数据并发送 boot 命令。</para>
    /// </summary>
    /// <param name="stream">Stream containing boot image data. <para>包含启动镜像数据的流。</para></param>
    /// <param name="length">Length of boot image data in bytes. <para>启动镜像数据字节长度。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Boot(Stream stream, long length)
    {
        DownloadData(stream, length).ThrowIfError();
        return RawCommand("boot");
    }

    /// <summary>
    /// Boots the device using a boot image file.
    /// Reads the boot image file and sends it to the device for booting.
    /// <para>使用启动镜像文件启动设备。
    /// 读取启动镜像文件并将其发送到设备以启动。</para>
    /// </summary>
    /// <param name="filePath">Path to the boot image file. <para>启动镜像文件的路径。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse BootFromFile(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return Boot(fs, fs.Length);
    }
}
