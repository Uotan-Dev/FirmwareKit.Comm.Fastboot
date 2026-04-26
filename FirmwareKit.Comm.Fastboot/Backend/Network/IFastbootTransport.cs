namespace FirmwareKit.Comm.Fastboot;


/// <summary>
/// Defines the basic transport interface for fastboot communication (read/write).
/// <para>定义 fastboot 通信的基本传输接口（读/写）。</para>
/// </summary>
public interface IFastbootTransport : IDisposable
{
    /// <summary>
    /// Reads data from the transport with the specified maximum length.
    /// <para>从传输层读取指定最大长度的数据。</para>
    /// </summary>
    byte[] Read(int length);

    /// <summary>
    /// Writes data to the transport, returning the number of bytes actually written.
    /// <para>向传输层写入数据，返回实际写入的字节数。</para>
    /// </summary>
    long Write(byte[] data, int length);
}

/// <summary>
/// Optional transport extension for reading directly into caller-provided buffers.
/// Implement this to avoid per-read byte[] allocations on hot paths.
/// <para>可选的传输扩展，用于直接读入调用方提供的缓冲区。
/// 实现此接口以避免热路径上每次读取的 byte[] 分配。</para>
/// </summary>
public interface IFastbootBufferedTransport : IFastbootTransport
{
    /// <summary>
    /// Reads data directly into the specified buffer, returning the number of bytes read.
    /// <para>将数据直接读入指定缓冲区，返回读取的字节数。</para>
    /// </summary>
    int ReadInto(byte[] buffer, int offset, int length);
}


