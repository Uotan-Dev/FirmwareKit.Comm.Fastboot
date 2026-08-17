namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sends a signature to the device for image verification.
    /// Downloads the signature data and sends the signature command.
    /// <para>向设备发送签名以进行镜像验证。
    /// 下载签名数据并发送签名命令。</para>
    /// </summary>
    /// <param name="sigData">Signature data bytes to send. <para>要发送的签名数据字节。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Signature(byte[] sigData)
    {
        DownloadData(sigData).ThrowIfError();
        return RawCommand("signature");
    }

    /// <summary>
    /// Sends a signature to the device for image verification from a stream.
    /// Downloads the signature data from stream and sends the signature command.
    /// <para>从流向设备发送签名以进行镜像验证。
    /// 从流下载签名数据并发送签名命令。</para>
    /// </summary>
    /// <param name="sigStream">Stream containing signature data. <para>包含签名数据的流。</para></param>
    /// <param name="length">Length of signature data in bytes. <para>签名数据字节长度。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Signature(Stream sigStream, long length)
    {
        DownloadData(sigStream, length).ThrowIfError();
        return RawCommand("signature");
    }

    /// <summary>
    /// Sends a signature file to the device for image verification.
    /// Reads the signature file and sends it to the device.
    /// <para>向设备发送签名文件以进行镜像验证。
    /// 读取签名文件并将其发送到设备。</para>
    /// </summary>
    /// <param name="filePath">Path to the signature file. <para>签名文件的路径。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Signature(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return Signature(fs, fs.Length);
    }
}
