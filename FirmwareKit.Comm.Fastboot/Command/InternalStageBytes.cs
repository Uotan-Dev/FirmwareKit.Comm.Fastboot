namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Stages data bytes to the device for later use with flash or boot commands.
    /// <para>将数据字节暂存到设备以供后续 flash 或 boot 命令使用。</para>
    /// </summary>
    /// <param name="data">Data bytes to stage. <para>要暂存的数据字节。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Stage(byte[] data)
    {
        NotifyCurrentStep("Staging data...");
        FastbootResponse downloadRes = DownloadData(data);
        if (downloadRes.Result != FastbootState.Success) return downloadRes;

        return RawCommand("stage");
    }

    /// <summary>
    /// Stages data from a stream to the device for later use with flash or boot commands.
    /// <para>从流将数据暂存到设备以供后续 flash 或 boot 命令使用。</para>
    /// </summary>
    /// <param name="stream">Stream containing data to stage. <para>包含要暂存数据的流。</para></param>
    /// <param name="length">Length of data in bytes. <para>数据字节长度。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Stage(Stream stream, long length)
    {
        NotifyCurrentStep("Staging data from stream...");
        FastbootResponse downloadRes = DownloadData(stream, length);
        if (downloadRes.Result != FastbootState.Success) return downloadRes;

        return RawCommand("stage");
    }

    /// <summary>
    /// Stages a file to the device for later use with flash or boot commands.
    /// <para>将文件暂存到设备以供后续 flash 或 boot 命令使用。</para>
    /// </summary>
    /// <param name="filePath">Path to the file to stage. <para>要暂存的文件路径。</para></param>
    /// <returns>A FastbootResponse indicating the result. <para>指示操作结果的 FastbootResponse。</para></returns>
    public FastbootResponse Stage(string filePath)
    {
        NotifyCurrentStep($"Staging file: {Path.GetFileName(filePath)}...");
        using var fs = File.OpenRead(filePath);
        return Stage(fs, fs.Length);
    }
}
