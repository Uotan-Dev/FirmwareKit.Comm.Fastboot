namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// Event arguments for data received from a fastboot device.
/// <para>从 fastboot 设备接收数据的事件参数。</para>
/// </summary>
public sealed class FastbootReceivedFromDeviceEventArgs : EventArgs
{
    /// <summary>
    /// Gets the type of the received data.
    /// <para>获取接收数据的类型。</para>
    /// </summary>
    public FastbootState Type { get; }

    /// <summary>
    /// Gets the INFO message content, if applicable.
    /// <para>获取 INFO 消息内容（如适用）。</para>
    /// </summary>
    public string? NewInfo { get; }

    /// <summary>
    /// Gets the TEXT message content, if applicable.
    /// <para>获取 TEXT 消息内容（如适用）。</para>
    /// </summary>
    public string? NewText { get; }

    /// <summary>
    /// Initializes a new instance with the specified state, info, and text.
    /// <para>使用指定的状态、信息和文本初始化新实例。</para>
    /// </summary>
    public FastbootReceivedFromDeviceEventArgs(FastbootState type, string? newInfo = null, string? newText = null)
    {
        Type = type;
        NewInfo = newInfo;
        NewText = newText;
    }
}
