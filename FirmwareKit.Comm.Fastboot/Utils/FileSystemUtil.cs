namespace FirmwareKit.Comm.Fastboot;

internal static class FileSystemUtil
{
    /// <summary>
    /// Generates a highly simplified empty EXT4 image
    /// <para>生成高度简化的空 EXT4 镜像。</para>
    /// </summary>
    /// <param name="path">Path to the output file. <para>输出文件的路径。</para></param>
    /// <param name="size">Size of the image in bytes. <para>镜像大小（字节）。</para></param>
    [ExternalToolDependency("mke2fs")]
    public static void CreateEmptyExt4(string path, long size)
    {
        if (size < 4096 * 10) throw new ArgumentException("Size too small for EXT4");

        using var fs = File.Create(path);
        fs.SetLength(size);

        byte[] sb = new byte[1024];

        uint blockSize = 4096;
        uint blockCount = (uint)(size / blockSize);

        BitConverter.GetBytes((uint)128).CopyTo(sb, 0);
        BitConverter.GetBytes(blockCount).CopyTo(sb, 4);
        BitConverter.GetBytes((uint)2).CopyTo(sb, 24);
        BitConverter.GetBytes((ushort)0xEF53).CopyTo(sb, 56);
        BitConverter.GetBytes((ushort)1).CopyTo(sb, 58);
        BitConverter.GetBytes((uint)1).CopyTo(sb, 76);
        BitConverter.GetBytes((ushort)256).CopyTo(sb, 88);

        fs.Seek(1024, SeekOrigin.Begin);
        fs.Write(sb, 0, sb.Length);
    }

    /// <summary>
    /// Generates a highly simplified empty F2FS image
    /// <para>生成高度简化的空 F2FS 镜像。</para>
    /// </summary>
    /// <param name="path">Path to the output file. <para>输出文件的路径。</para></param>
    /// <param name="size">Size of the image in bytes. <para>镜像大小（字节）。</para></param>
    [ExternalToolDependency("make_f2fs")]
    public static void CreateEmptyF2fs(string path, long size)
    {
        if (size < 1024 * 1024 * 2) throw new ArgumentException("Size too small for F2FS");

        using var fs = File.Create(path);
        fs.SetLength(size);

        byte[] sb = new byte[1024];
        BitConverter.GetBytes(0xF2F52010).CopyTo(sb, 0);

        fs.Seek(1024, SeekOrigin.Begin);
        fs.Write(sb, 0, sb.Length);
    }


}
