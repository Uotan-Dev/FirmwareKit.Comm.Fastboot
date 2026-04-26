namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// EXT4 sparse file header structure.
/// <para>EXT4 稀疏文件头结构。</para>
/// </summary>
public struct Ext4FileHeader
{
    public uint Magic;
    public ushort MajorVersion;
    public ushort MinorVersion;
    public ushort FileHeaderSize;
    public ushort ChunkHeaderSize;
    public uint BlockSize;
    public uint TotalBlocks;
    public uint TotalChunks;
    public uint CRC32;
}

/// <summary>
/// EXT4 sparse file chunk header structure.
/// <para>EXT4 稀疏文件块头结构。</para>
/// </summary>
public struct Ext4ChunkHeader
{
    public ushort Type;
    public ushort Reserved;
    public uint ChunkSize;
    public uint TotalSize;
}

