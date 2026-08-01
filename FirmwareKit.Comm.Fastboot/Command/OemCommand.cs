namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sends an OEM-specific command to the device.
    /// <para>向设备发送 OEM 特定命令。</para>
    /// </summary>
    public FastbootResponse OemCommand(string oemCmd) => RawCommand("oem " + oemCmd);

    /// <summary>
    /// Executes an arbitrary U-Boot command synchronously (U-Boot fastboot extension,
    /// requires CONFIG_FASTBOOT_OEM_RUN on the device).
    /// <para>同步执行任意 U-Boot 命令（U-Boot fastboot 扩展，设备需开启
    /// CONFIG_FASTBOOT_OEM_RUN）。</para>
    /// </summary>
    public FastbootResponse OemUcmd(string ubootCommand) => RawCommand("oem ucmd " + ubootCommand);

    /// <summary>
    /// Executes an arbitrary U-Boot command asynchronously without waiting for it to finish
    /// (U-Boot fastboot extension, requires CONFIG_FASTBOOT_OEM_RUN on the device).
    /// <para>异步执行任意 U-Boot 命令，不等待其完成（U-Boot fastboot 扩展，
    /// 设备需开启 CONFIG_FASTBOOT_OEM_RUN）。</para>
    /// </summary>
    public FastbootResponse OemAcmd(string ubootCommand) => RawCommand("oem acmd " + ubootCommand);

    /// <summary>
    /// Executes a U-Boot fastboot oem run command (alias of <see cref="OemUcmd"/> for
    /// bootloaders exposing "oem run" directly).
    /// <para>执行 U-Boot fastboot oem run 命令（<see cref="OemUcmd"/> 的别名，
    /// 用于直接暴露 "oem run" 的 bootloader）。</para>
    /// </summary>
    public FastbootResponse OemRun(string ubootCommand) => RawCommand("oem run " + ubootCommand);
}
