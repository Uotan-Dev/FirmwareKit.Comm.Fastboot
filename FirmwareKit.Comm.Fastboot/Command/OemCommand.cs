namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sends an OEM-specific command to the device.
    /// <para>向设备发送 OEM 特定命令。</para>
    /// </summary>
    public FastbootResponse OemCommand(string oemCmd) => RawCommand("oem " + oemCmd);
}
