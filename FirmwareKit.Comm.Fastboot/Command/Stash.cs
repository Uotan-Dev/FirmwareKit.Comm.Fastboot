namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Stashes byte data to the device under the specified name.
    /// <para>将字节数据暂存到设备上指定名称下。</para>
    /// </summary>
    public FastbootResponse Stash(string name, byte[] data)
    {
        NotifyCurrentStep($"Stashing data to '{name}'...");
        FastbootResponse downloadRes = DownloadData(data);
        if (downloadRes.Result != FastbootState.Success) return downloadRes;

        return RawCommand("stash:" + name);
    }

    /// <summary>
    /// Stashes stream data to the device under the specified name.
    /// <para>将流数据暂存到设备上指定名称下。</para>
    /// </summary>
    public FastbootResponse Stash(string name, Stream stream, long length)
    {
        NotifyCurrentStep($"Stashing data to '{name}' from stream...");
        FastbootResponse downloadRes = DownloadData(stream, length);
        if (downloadRes.Result != FastbootState.Success) return downloadRes;

        return RawCommand("stash:" + name);
    }

    /// <summary>
    /// Stashes a file to the device under the specified name.
    /// <para>将文件暂存到设备上指定名称下。</para>
    /// </summary>
    public FastbootResponse Stash(string name, string filePath)
    {
        NotifyCurrentStep($"Stashing file to '{name}'...");
        using var fs = File.OpenRead(filePath);
        return Stash(name, fs, fs.Length);
    }
}
