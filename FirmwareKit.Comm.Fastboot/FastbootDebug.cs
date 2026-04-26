namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// Provides debug logging for fastboot operations, controlled by the FASTBOOT_DEBUG environment variable.
/// <para>提供 fastboot 操作的调试日志，由 FASTBOOT_DEBUG 环境变量控制。</para>
/// </summary>
public static class FastbootDebug
{
    private static bool? _debugEnabled;

    /// <summary>
    /// Optional output handler for debug messages. If null, messages are discarded.
    /// <para>调试消息的可选输出处理器。如果为 null，消息将被丢弃。</para>
    /// </summary>
    public static Action<string>? Output;

    /// <summary>
    /// Gets or sets whether debug logging is enabled. Defaults to the FASTBOOT_DEBUG environment variable.
    /// <para>获取或设置是否启用调试日志。默认由 FASTBOOT_DEBUG 环境变量决定。</para>
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            if (_debugEnabled == null)
            {
                _debugEnabled = Environment.GetEnvironmentVariable("FASTBOOT_DEBUG") == "1";
            }
            return _debugEnabled.Value;
        }
        set => _debugEnabled = value;
    }

    /// <summary>
    /// Logs a debug message if debug mode is enabled.
    /// <para>如果调试模式已启用，则记录调试消息。</para>
    /// </summary>
    public static void Log(string message)
    {
        if (IsEnabled)
        {
            Output?.Invoke(message);
        }
    }
}
