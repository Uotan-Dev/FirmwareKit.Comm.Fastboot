namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sends a GSI (Generic System Image) sub-command to the device.
    /// <para>向设备发送 GSI（通用系统镜像）子命令。</para>
    /// </summary>
    public FastbootResponse GsiCommand(string subCmd) => RawCommand("gsi:" + subCmd);
}
