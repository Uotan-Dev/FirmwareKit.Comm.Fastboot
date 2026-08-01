using FirmwareKit.Comm.Usb.Abstractions;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Fastboot.Usb;

/// <summary>
/// Manages USB fastboot device discovery and enumeration.
/// <para>管理 USB fastboot 设备的发现和枚举。</para>
/// </summary>
public static class UsbManager
{
    /// <summary>
    /// Gets or sets whether to force the use of libusb-dotnet instead of native USB APIs.
    /// Defaults to true on Linux.
    /// <para>获取或设置是否强制使用 libusb-dotnet 而非原生 USB API。
    /// 在 Linux 上默认为 true。</para>
    /// </summary>
    public static bool ForceLibUsb { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Gets or sets the USB device matching mode used during enumeration.
    /// Defaults to <see cref="UsbMatchMode.InterfaceOnly"/> (standard fastboot interface only).
    /// <para>获取或设置枚举时使用的 USB 设备匹配模式。
    /// 默认为 <see cref="UsbMatchMode.InterfaceOnly"/>（仅标准 fastboot 接口）。</para>
    /// </summary>
    public static UsbMatchMode MatchMode { get; set; } = UsbMatchMode.InterfaceOnly;

    /// <summary>
    /// Gets the built-in known fastboot device profiles.
    /// <para>获取内置的已知 fastboot 设备档案。</para>
    /// </summary>
    public static IReadOnlyList<FastbootDeviceProfile> DefaultDeviceProfiles { get; } =
    [
        // Amazon Kindle Fire (1st gen, stock bootloader) exposes fastboot with a
        // non-standard interface descriptor in some firmware revisions.
        new(0x1949, 0x0006, "Amazon Kindle Fire (stock bootloader)"),
        // Amazon Kindle Fire with FireFireFire bootloader uses the Google VID.
        new(0x18D1, 0x0100, "Amazon Kindle Fire (FFF bootloader)"),
        // Fuzhou Rockchip Electronics: U-Boot fastboot gadget (e.g. RK3xxx dev boards).
        new(0x2207, null, "Fuzhou Rockchip Electronics (U-Boot fastboot)"),
        // Allwinner Technology: U-Boot fastboot gadget on many dev boards.
        new(0x1F3A, null, "Allwinner Technology (U-Boot fastboot)"),
        // Huawei / Honor: fastboot mode on HarmonyOS and Android devices.
        new(0x12D1, null, "Huawei / Honor (HarmonyOS/Android fastboot)"),
    ];

    /// <summary>
    /// Gets or sets the known fastboot device profiles (VID/PID whitelist) used as a discovery
    /// fallback when <see cref="MatchMode"/> is <see cref="UsbMatchMode.InterfaceOrKnownVidPid"/>.
    /// <para>获取或设置已知 fastboot 设备档案（VID/PID 白名单），当 <see cref="MatchMode"/>
    /// 为 <see cref="UsbMatchMode.InterfaceOrKnownVidPid"/> 时用作发现兜底。</para>
    /// </summary>
    public static IReadOnlyList<FastbootDeviceProfile> KnownDeviceProfiles { get; set; } = DefaultDeviceProfiles;

    /// <summary>
    /// Loads device profiles from a JSON manifest (see devices.json) and replaces
    /// <see cref="KnownDeviceProfiles"/>. Malformed or missing entries are skipped, so a
    /// partial manifest never breaks discovery; a missing file keeps the built-in defaults.
    /// <para>从 JSON 清单（参见 devices.json）加载设备档案并替换
    /// <see cref="KnownDeviceProfiles"/>。格式错误或缺失的条目会被跳过，因此部分损坏的清单
    /// 不会破坏设备发现；文件缺失时保留内置默认档案。</para>
    /// </summary>
    /// <param name="path">Path to the JSON manifest. JSON 清单路径。</param>
    public static void LoadDeviceProfilesFromFile(string path)
    {
        var loaded = DeviceProfileLoader.LoadFromFile(path);
        if (loaded.Count > 0)
        {
            KnownDeviceProfiles = loaded;
        }
    }

    private static readonly global::FirmwareKit.Comm.IFirmwareKitComm Comm = new global::FirmwareKit.Comm.FirmwareKitComm();

    /// <summary>
    /// Enumerates all connected fastboot USB devices.
    /// <para>枚举所有已连接的 fastboot USB 设备。</para>
    /// </summary>
    public static List<UsbDevice> GetAllDevices()
    {
        try
        {
            var apiKind = ForceLibUsb ? UsbApiKind.LibUsbDotNet : UsbApiKind.Native;

            var result = new List<UsbDevice>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (MatchMode is UsbMatchMode.InterfaceOnly or UsbMatchMode.InterfaceOrKnownVidPid)
            {
                // Primary pass: standard fastboot interface (class 0xff, subclass 0x42, protocol 0x03),
                // matching AOSP match_fastboot behavior.
                var interfaceFilter = new UsbDeviceFilter
                {
                    InterfaceClass = 0xFF,
                    InterfaceSubClass = 0x42,
                    InterfaceProtocol = 0x03,
                };
                AddDevices(Comm.EnumerateUsbDevices(apiKind, interfaceFilter), apiKind, seenPaths, result);
            }

            if (MatchMode == UsbMatchMode.InterfaceOrKnownVidPid)
            {
                // Fallback pass: known VID/PID profiles whose interface descriptor is not
                // the standard fastboot one. Enumerate by VID/PID without interface constraints
                // so legacy devices (e.g. Kindle Fire) are still discovered.
                foreach (var profile in KnownDeviceProfiles)
                {
                    var profileFilter = new UsbDeviceFilter
                    {
                        VendorId = profile.VendorId,
                        ProductId = profile.ProductId,
                    };
                    AddDevices(Comm.EnumerateUsbDevices(apiKind, profileFilter), apiKind, seenPaths, result);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to enumerate fastboot devices via FirmwareKit.Comm.", ex);
        }
    }

    private static void AddDevices(
        IReadOnlyList<UsbDeviceInfo> discovered,
        UsbApiKind apiKind,
        HashSet<string> seenPaths,
        List<UsbDevice> result)
    {
        foreach (var info in discovered)
        {
            // Deduplicate across the interface pass and the VID/PID fallback pass.
            string key = string.IsNullOrWhiteSpace(info.DevicePath) ? info.DeviceKey : info.DevicePath;
            if (!string.IsNullOrWhiteSpace(key) && !seenPaths.Add(key))
            {
                continue;
            }

            var device = new CommUsbDevice(Comm, info, ForceLibUsb);
            if (device.CreateHandle() == 0)
            {
                result.Add(device);
            }
            else
            {
                device.Dispose();
            }
        }
    }
}
