namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Continues device boot, exiting fastboot mode.
    /// <para>继续设备启动，退出 fastboot 模式。</para>
    /// </summary>
    public FastbootResponse Continue() => RawCommand("continue");
}
