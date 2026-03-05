namespace FirmwareKit.Comm.Fastboot;

public static class FastbootDebug
{
    private static bool? _debugEnabled;

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

    public static void Log(string message)
    {
        if (IsEnabled)
        {
            Console.Error.WriteLine($"[DEBUG] {message}");
        }
    }
}
