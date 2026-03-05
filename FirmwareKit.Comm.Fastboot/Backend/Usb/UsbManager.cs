using FirmwareKit.Comm.Fastboot.Backend.Usb.libusbdotnet;
using FirmwareKit.Comm.Fastboot.Backend.Usb.Linux;
using FirmwareKit.Comm.Fastboot.Backend.Usb.macOS;
using FirmwareKit.Comm.Fastboot.Backend.Usb.Windows;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Fastboot.Backend.Usb;

public static class UsbManager
{
    public static bool ForceLibUsb { get; set; } = false;

    public static List<UsbDevice> GetAllDevices()
    {
        if (ForceLibUsb)
        {
            try
            {
                return LibUsbFinder.FindDevice();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to use libusb. Ensure libusb is properly installed and configured.", ex);
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return WinUSBFinder.FindDevice();
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return LinuxUsbFinder.FindDevice();
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return MacOSUsbFinder.FindDevice();
        }
        try
        {
            return LibUsbFinder.FindDevice();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Fallback to libusb failed. Ensure libusb is properly installed and configured.", ex);
        }
    }


}



