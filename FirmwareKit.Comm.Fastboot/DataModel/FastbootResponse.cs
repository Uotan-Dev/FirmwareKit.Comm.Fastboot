namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// Represents a response received from a fastboot device.
/// <para>表示从 fastboot 设备接收到的响应。</para>
/// </summary>
public class FastbootResponse
{
    /// <summary>
    /// Gets or sets the result state of the fastboot response.
    /// <para>获取或设置 fastboot 响应的结果状态。</para>
    /// </summary>
    public FastbootState Result { get; set; }

    /// <summary>
    /// Gets or sets the response text string from the device.
    /// <para>获取或设置设备的响应文本字符串。</para>
    /// </summary>
    public string Response { get; set; } = "";

    /// <summary>
    /// Gets or sets the raw data payload, if any.
    /// <para>获取或设置原始数据负载（如果有）。</para>
    /// </summary>
    public byte[]? Data { get; set; }

    /// <summary>
    /// Gets or sets the size of the data payload.
    /// <para>获取或设置数据负载的大小。</para>
    /// </summary>
    public long DataSize { get; set; }

    /// <summary>
    /// Gets or sets the list of INFO lines received from the device.
    /// <para>获取或设置从设备接收的 INFO 行列表。</para>
    /// </summary>
    public List<string> Info { get; set; } = [];

    /// <summary>
    /// Gets or sets the TEXT content received from the device.
    /// <para>获取或设置从设备接收的 TEXT 内容。</para>
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Gets or sets the CRC hash value for data verification, if available.
    /// <para>获取或设置用于数据校验的 CRC 哈希值（如果可用）。</para>
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Throws an exception if the response indicates a failure or timeout.
    /// <para>如果响应表示失败或超时，则抛出异常。</para>
    /// </summary>
    public FastbootResponse ThrowIfError()
    {
        if (Result is FastbootState.Fail or FastbootState.Timeout)
            throw new Exception($"Command failed: {Result} - {Response}");
        return this;
    }
}

/// <summary>
/// Represents the state of a fastboot protocol response.
/// <para>表示 fastboot 协议响应的状态。</para>
/// </summary>
public enum FastbootState
{
    /// <summary>The command completed successfully (OKAY). <para>命令成功完成（OKAY）。</para></summary>
    Success,
    /// <summary>The device reported a failure (FAIL). <para>设备报告失败（FAIL）。</para></summary>
    Fail,
    /// <summary>The device sent a TEXT frame. <para>设备发送了 TEXT 帧。</para></summary>
    Text,
    /// <summary>The device sent a DATA frame announcing a payload. <para>设备发送了 DATA 帧，声明后续负载。</para></summary>
    Data,
    /// <summary>The device sent an INFO frame. <para>设备发送了 INFO 帧。</para></summary>
    Info,
    /// <summary>The device sent an unrecognized status code. <para>设备发送了无法识别的状态码。</para></summary>
    Unknown,
    /// <summary>No response was received before the timeout. <para>在超时前未收到响应。</para></summary>
    Timeout
}

