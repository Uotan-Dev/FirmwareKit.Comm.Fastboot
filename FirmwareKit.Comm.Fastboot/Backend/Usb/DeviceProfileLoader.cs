namespace FirmwareKit.Comm.Fastboot.Usb;

/// <summary>
/// Loads fastboot device profiles from a JSON file (see devices.json).
/// Uses a lightweight, AOT-safe parser so the library stays compatible with
/// NativeAOT and trimming (no reflection-based System.Text.Json).
/// <para>从 JSON 文件加载 fastboot 设备档案（参见 devices.json）。
/// 使用轻量、AOT 安全的解析器，保证库在 NativeAOT 与裁剪下兼容
/// （不依赖基于反射的 System.Text.Json）。</para>
/// </summary>
public static class DeviceProfileLoader
{
    /// <summary>
    /// Loads device profiles from the specified JSON file. Entries without a vendor id,
    /// malformed entries and missing files are skipped, so a partially broken manifest
    /// never breaks discovery.
    /// <para>从指定 JSON 文件加载设备档案。缺少厂商 ID、格式错误的条目以及缺失的文件会被跳过，
    /// 因此部分损坏的清单不会破坏设备发现。</para>
    /// </summary>
    /// <param name="path">Path to the JSON manifest. JSON 清单路径。</param>
    /// <returns>The loaded profiles. 加载到的设备档案。</returns>
    public static List<FastbootDeviceProfile> LoadFromFile(string path)
    {
        var profiles = new List<FastbootDeviceProfile>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return profiles;

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch
        {
            return profiles;
        }

        try
        {
            // Expected shape: { "profiles": [ { "name": "...", "vendorId": "0x18D1", "productId": "0x0006" }, ... ] }
            foreach (var entry in ParseObjects(FindArray(json, "profiles")))
            {
                try
                {
                    string? name = GetStringValue(entry, "name");
                    string? vendorText = GetStringValue(entry, "vendorId");
                    string? productText = GetStringValue(entry, "productId");

                    if (!TryParseHex16(vendorText, out ushort vendorId) || vendorId == 0) continue;

                    ushort? productId = null;
                    if (!string.IsNullOrWhiteSpace(productText) && TryParseHex16(productText, out ushort parsedPid))
                    {
                        productId = parsedPid;
                    }

                    string profileName = string.IsNullOrWhiteSpace(name) ? $"VID 0x{vendorId:X4}" : name!;
                    profiles.Add(new FastbootDeviceProfile(vendorId, productId, profileName));
                }
                catch
                {
                    // Skip malformed entries; a broken manifest must not break discovery.
                }
            }
        }
        catch
        {
            // Unreadable manifest falls back to the built-in defaults.
        }

        return profiles;
    }

    private static bool TryParseHex16(string? text, out ushort value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string nonNull = text!;
        string hex = nonNull.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? nonNull.Substring(2) : nonNull;
        return ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Locates the first '[' ... ']' array that belongs to the given member name.
    /// <para>定位属于指定成员名的第一个 '[' ... ']' 数组。</para>
    /// </summary>
    private static string FindArray(string json, string memberName)
    {
        int nameIndex = json.IndexOf("\"" + memberName + "\"", StringComparison.OrdinalIgnoreCase);
        if (nameIndex < 0) return "";

        int bracket = json.IndexOf('[', nameIndex);
        if (bracket < 0) return "";

        int depth = 0;
        bool inString = false;
        for (int i = bracket; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '"' && (i == 0 || json[i - 1] != '\\')) inString = !inString;
            if (inString) continue;

            if (c == '[') depth++;
            else if (c == ']')
            {
                depth--;
                if (depth == 0) return json.Substring(bracket, i - bracket + 1);
            }
        }
        return "";
    }

    /// <summary>
    /// Splits a '[...]' array into its top-level '{...}' object strings.
    /// <para>将 '[...]' 数组拆分为其顶层 '{...}' 对象字符串。</para>
    /// </summary>
    private static IEnumerable<string> ParseObjects(string arrayJson)
    {
        if (string.IsNullOrEmpty(arrayJson)) yield break;

        int i = 0;
        while (i < arrayJson.Length)
        {
            int open = arrayJson.IndexOf('{', i);
            if (open < 0) yield break;

            int depth = 0;
            bool inString = false;
            int close = -1;
            for (int j = open; j < arrayJson.Length; j++)
            {
                char c = arrayJson[j];
                if (c == '"' && (j == 0 || arrayJson[j - 1] != '\\')) inString = !inString;
                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        close = j;
                        break;
                    }
                }
            }

            if (close < 0) yield break;
            yield return arrayJson.Substring(open, close - open + 1);
            i = close + 1;
        }
    }

    /// <summary>
    /// Reads the string value of a member inside a single '{...}' object string.
    /// <para>在单个 '{...}' 对象字符串中读取指定成员名的字符串值。</para>
    /// </summary>
    private static string? GetStringValue(string objectJson, string memberName)
    {
        int nameIndex = objectJson.IndexOf("\"" + memberName + "\"", StringComparison.OrdinalIgnoreCase);
        if (nameIndex < 0) return null;

        int colon = objectJson.IndexOf(':', nameIndex);
        if (colon < 0) return null;

        int i = colon + 1;
        while (i < objectJson.Length && char.IsWhiteSpace(objectJson[i])) i++;
        if (i >= objectJson.Length || objectJson[i] != '"') return null;

        i++;
        var sb = new System.Text.StringBuilder();
        while (i < objectJson.Length)
        {
            char c = objectJson[i];
            if (c == '\\' && i + 1 < objectJson.Length)
            {
                sb.Append(objectJson[i + 1]);
                i += 2;
                continue;
            }
            if (c == '"') return sb.ToString();
            sb.Append(c);
            i++;
        }
        return null;
    }
}
