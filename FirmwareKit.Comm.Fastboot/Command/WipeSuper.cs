namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Wipes the super partition metadata.
    /// <para>清除 super 分区元数据。</para>
    /// </summary>
    public FastbootResponse WipeSuper(string partition) => RawCommand("wipe-super:" + partition);
}
