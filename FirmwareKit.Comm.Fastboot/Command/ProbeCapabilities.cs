namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Gets the last probed device capabilities, or null when <see cref="ProbeCapabilities"/> has not been called.
    /// <para>获取最近探测的设备能力信息，未调用 <see cref="ProbeCapabilities"/> 时为 null。</para>
    /// </summary>
    public DeviceCapabilities? Capabilities { get; private set; }

    /// <summary>
    /// Probes the connected device for its supported feature set using lightweight getvar queries.
    /// Safe to call on any fastboot device: unsupported variables simply leave the corresponding
    /// capability unset, so callers can degrade gracefully (e.g. U-Boot boards, legacy bootloaders).
    /// <para>通过轻量 getvar 查询探测已连接设备支持的特性集。
    /// 可在任何 fastboot 设备上安全调用：不支持的变量仅使对应能力信息保持未设置，
    /// 调用方可以据此优雅降级（例如 U-Boot 开发板、老旧 bootloader）。</para>
    /// </summary>
    /// <returns>The probed capabilities. 探测到的能力信息。</returns>
    public DeviceCapabilities ProbeCapabilities()
    {
        var caps = new DeviceCapabilities
        {
            ProbedAtUtc = DateTime.UtcNow,
            IsProbed = true,
        };

        if (TryGetVar("version", out string? version) && !string.IsNullOrEmpty(version))
        {
            caps.ProtocolVersion = version;
        }
        if (TryGetVar("version-bootloader", out string? bootloader) && !string.IsNullOrEmpty(bootloader))
        {
            caps.BootloaderVersion = bootloader;
        }

        if (TryGetVar("max-download-size", out string? maxSize) &&
            TryParseNumericSize(maxSize, out long parsedMax) && parsedMax > 0)
        {
            caps.MaxDownloadSize = parsedMax;
        }

        // A device that replies FAIL to an unknown variable yields an empty string here,
        // which must be treated as "not reported" instead of a real (empty) value.
        if (TryGetVar("is-userspace", out string? userspace) && !string.IsNullOrEmpty(userspace))
        {
            caps.IsUserspace = userspace == "yes";
        }
        if (TryGetVar("has-crc", out string? crc) && !string.IsNullOrEmpty(crc))
        {
            caps.HasCrc = crc == "yes";
        }
        if (TryGetVar("has-slot:boot", out string? hasSlot) && !string.IsNullOrEmpty(hasSlot))
        {
            caps.SupportsSlots = hasSlot == "yes";
        }
        if (TryGetVar("slot-count", out string? slotCount) && int.TryParse(slotCount, out int slots))
        {
            caps.SlotCount = slots;
        }
        if (TryGetVar("current-slot", out string? currentSlot) && !string.IsNullOrEmpty(currentSlot))
        {
            caps.CurrentSlot = currentSlot.StartsWith("_") ? currentSlot.Substring(1) : currentSlot;
        }
        if (TryGetVar("super-partition-name", out string? superName) && !string.IsNullOrEmpty(superName))
        {
            caps.SuperPartitionName = superName;
        }

        // "is-logical" support means the device implements dynamic partition metadata.
        // A non-empty "yes"/"no" answer proves the variable is supported; an empty value
        // (device replied FAIL to the unknown variable) means it is not.
        bool logicalSupported = false;
        if (TryGetVar("is-logical:super", out string? logicalSuper) && !string.IsNullOrEmpty(logicalSuper))
        {
            logicalSupported = true;
        }
        else if (TryGetVar("is-logical:boot", out string? logicalBoot) && !string.IsNullOrEmpty(logicalBoot))
        {
            logicalSupported = true;
        }
        caps.SupportsLogicalPartitions = logicalSupported;

        Capabilities = caps;
        return caps;
    }

    private static bool TryParseNumericSize(string text, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(text.Substring(2), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }
        return long.TryParse(text, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
