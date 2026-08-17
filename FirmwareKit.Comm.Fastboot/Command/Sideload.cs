namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    private const string SideloadNotSupportedMessage =
        "fastboot sideload is not part of the fastboot wire protocol. OTA sideload is a " +
        "recovery/adb feature ('adb sideload package.zip'), not a fastboot command. The previous " +
        "implementation downloaded the package and issued 'flash:recovery', which corrupts the " +
        "recovery partition. Use 'adb sideload' from recovery instead.";

    /// <summary>
    /// Sideloading an OTA update package is not supported over the fastboot protocol.
    /// <para>fastboot 协议不支持旁加载 OTA 更新包。</para>
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown; use 'adb sideload' from recovery.</exception>
    public FastbootResponse Sideload(string zipPath)
        => throw new NotSupportedException(SideloadNotSupportedMessage);

    /// <summary>
    /// Sideloading an OTA update package is not supported over the fastboot protocol.
    /// <para>fastboot 协议不支持旁加载 OTA 更新包。</para>
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown; use 'adb sideload' from recovery.</exception>
    public FastbootResponse Sideload(Stream stream, long length)
        => throw new NotSupportedException(SideloadNotSupportedMessage);
}
