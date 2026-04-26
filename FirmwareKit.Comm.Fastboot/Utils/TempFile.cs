namespace FirmwareKit.Comm.Fastboot;

internal sealed class TempFile : IDisposable
{
    private bool _disposed;

    public string FilePath { get; }

    public TempFile(string prefix, string extension)
    {
        FilePath = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N") + extension);
    }

    public Stream OpenRead()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        return File.OpenRead(FilePath);
    }

    public Stream OpenWrite()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        return File.Create(FilePath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
    }
}
