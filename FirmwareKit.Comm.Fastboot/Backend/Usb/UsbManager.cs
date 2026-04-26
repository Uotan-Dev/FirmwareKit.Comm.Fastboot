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
            var filter = new UsbDeviceFilter
            {
                InterfaceClass = 0xFF,
                InterfaceSubClass = 0x42,
                InterfaceProtocol = 0x03,
            };

            var discovered = Comm.EnumerateUsbDevices(apiKind, filter);
            var result = new List<UsbDevice>();

            foreach (var info in discovered)
            {
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

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to enumerate fastboot devices via FirmwareKit.Comm.", ex);
        }
    }


}



