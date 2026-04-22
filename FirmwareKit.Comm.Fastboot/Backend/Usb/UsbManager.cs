using FirmwareKit.Comm.Usb.Abstractions;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Fastboot.Usb;

public static class UsbManager
{
    public static bool ForceLibUsb { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    private static readonly global::FirmwareKit.Comm.IFirmwareKitComm Comm = new global::FirmwareKit.Comm.FirmwareKitComm();

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



