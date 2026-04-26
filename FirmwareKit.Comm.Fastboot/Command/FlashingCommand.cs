namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Sends a flashing sub-command to the device (e.g., unlock, lock, get_unlock_ability).
    /// <para>向设备发送 flashing 子命令（如 unlock、lock、get_unlock_ability）。</para>
    /// </summary>
    public FastbootResponse FlashingCommand(string subCmd) => RawCommand("flashing " + subCmd);
}
