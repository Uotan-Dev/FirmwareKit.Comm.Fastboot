namespace FirmwareKit.Comm.Fastboot;


public interface IFastbootTransport : IDisposable
{
    byte[] Read(int length);
    long Write(byte[] data, int length);
}

/// <summary>
/// Optional transport extension for reading directly into caller-provided buffers.
/// Implement this to avoid per-read byte[] allocations on hot paths.
/// </summary>
public interface IFastbootBufferedTransport : IFastbootTransport
{
    int ReadInto(byte[] buffer, int offset, int length);
}


