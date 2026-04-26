namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Disables the GSI on the device.
    /// <para>禁用设备上的 GSI。</para>
    /// </summary>
    public FastbootResponse GsiDisable() => GsiCommand("disable");
}
