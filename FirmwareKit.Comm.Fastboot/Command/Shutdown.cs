namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Shuts down the device.
    /// <para>关闭设备。</para>
    /// </summary>
    public FastbootResponse Shutdown() => RawCommand("shutdown");
}
