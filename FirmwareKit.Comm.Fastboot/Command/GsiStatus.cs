namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Queries the GSI installation status on the device.
    /// <para>查询设备上的 GSI 安装状态。</para>
    /// </summary>
    public FastbootResponse GsiStatus() => GsiCommand("status");
}
