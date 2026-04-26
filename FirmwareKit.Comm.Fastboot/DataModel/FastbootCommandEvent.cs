namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// Event arguments for a completed fastboot command.
/// <para>已完成 fastboot 命令的事件参数。</para>
/// </summary>
public sealed class FastbootCommandEventArgs : EventArgs
{
    /// <summary>
    /// Gets the command string that was executed.
    /// <para>获取已执行的命令字符串。</para>
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Gets the response received from the device.
    /// <para>获取从设备接收的响应。</para>
    /// </summary>
    public FastbootResponse Response { get; }

    /// <summary>
    /// Gets whether the command was executed in quiet mode (suppressing notifications).
    /// <para>获取命令是否以静默模式执行（抑制通知）。</para>
    /// </summary>
    public bool Quiet { get; }

    /// <summary>
    /// Initializes a new instance with the specified command, response, and quiet flag.
    /// <para>使用指定的命令、响应和静默标志初始化新实例。</para>
    /// </summary>
    public FastbootCommandEventArgs(string command, FastbootResponse response, bool quiet)
    {
        Command = command;
        Response = response;
        Quiet = quiet;
    }
}
