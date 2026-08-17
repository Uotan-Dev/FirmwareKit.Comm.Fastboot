namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Uploads a file from the device and saves it to the specified output path.
    /// <para>从设备上传文件并保存到指定输出路径。</para>
    /// </summary>
    public FastbootResponse Upload(string filename, string outputPath)
    {
        using var fs = File.Create(outputPath);
        return UploadToStream(filename, fs);
    }

    /// <summary>
    /// Uploads a file from the device and writes it to the specified stream.
    /// <para>从设备上传文件并写入指定流。</para>
    /// </summary>
    public FastbootResponse UploadToStream(string filename, Stream output) => UploadData("upload:" + filename, output);

    /// <summary>
    /// Uploads a file from the device and returns its contents as a byte array.
    /// <para>从设备上传文件并以字节数组形式返回其内容。</para>
    /// </summary>
    public byte[] UploadToBytes(string filename)
    {
        using var ms = new MemoryStream();
        var res = UploadData("upload:" + filename, ms);
        if (res.Result != FastbootState.Success)
        {
            throw new Exception("upload failed: " + res.Response);
        }
        return ms.ToArray();
    }
}
