namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sideloads an update package from a ZIP file path.
    /// <para>从 ZIP 文件路径旁加载更新包。</para>
    /// </summary>
    public FastbootResponse Sideload(string zipPath)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException(zipPath);

        NotifyCurrentStep($"Sideloading {Path.GetFileName(zipPath)}...");
        using var fs = File.OpenRead(zipPath);
        return Sideload(fs, fs.Length);
    }

    /// <summary>
    /// Sideloads an update package from a stream.
    /// <para>从流旁加载更新包。</para>
    /// </summary>
    public FastbootResponse Sideload(Stream stream, long length)
    {
        NotifyCurrentStep("Sideloading update package...");
        var downloadRes = DownloadData(stream, length);
        if (downloadRes.Result != FastbootState.Success) return downloadRes;

        return RawCommand("flash:recovery");
    }
}
