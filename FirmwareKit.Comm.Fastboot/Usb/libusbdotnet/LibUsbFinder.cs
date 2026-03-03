using LibUsbDotNet.LibUsb;

namespace FirmwareKit.Comm.Fastboot.Usb.libusbdotnet
{
    public class LibUsbFinder
    {
        private static bool HasFastbootInterface(LibUsbDotNet.LibUsb.UsbDevice device)
        {
            try
            {
                foreach (var config in device.Configs)
                {
                    foreach (var ifc in config.Interfaces)
                    {
                        bool isFastboot = (int)ifc.Class == 0xff && (int)ifc.SubClass == 0x42 && (int)ifc.Protocol == 0x03;
                        if (!isFastboot) continue;

                        bool hasIn = false;
                        bool hasOut = false;
                        foreach (var endpoint in ifc.Endpoints)
                        {
                            if ((endpoint.EndpointAddress & 0x80) != 0) hasIn = true;
                            else hasOut = true;
                        }

                        if (hasIn && hasOut)
                            return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static List<UsbDevice> FindDevice()
        {
            List<UsbDevice> devices = new List<UsbDevice>();
            using (var context = new UsbContext())
            {
                var deviceList = context.List();

                foreach (var device in deviceList)
                {
                    var libUsbDevice = device as LibUsbDotNet.LibUsb.UsbDevice;
                    if (libUsbDevice == null) continue;
                    if (!HasFastbootInterface(libUsbDevice)) continue;

                    byte busNumber = libUsbDevice?.BusNumber ?? 0;
                    byte address = libUsbDevice?.Address ?? 0;

                    var fastbootDevice = new LibUsbDevice
                    {
                        Vid = (ushort)device.VendorId,
                        Pid = (ushort)device.ProductId,
                        BusNumber = busNumber,
                        DeviceAddress = address,
                        InterfaceId = 0,
                        DevicePath = $"Bus {busNumber} Device {address}: {device.VendorId:X4}:{device.ProductId:X4}",
                        UsbDeviceType = UsbDeviceType.LibUSB
                    };

                    if (fastbootDevice.CreateHandle() == 0)
                    {
                        devices.Add(fastbootDevice);
                    }
                    else
                    {
                        fastbootDevice.Dispose();
                    }
                }
            }
            return devices;
        }
    }
}
