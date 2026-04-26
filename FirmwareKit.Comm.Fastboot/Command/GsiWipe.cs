namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Wipes the GSI installation on the device.
    /// <para>清除设备上的 GSI 安装。</para>
    /// </summary>
    public FastbootResponse GsiWipe() => GsiCommand("wipe");
}
