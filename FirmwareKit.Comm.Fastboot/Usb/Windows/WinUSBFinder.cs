using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static FirmwareKit.Comm.Fastboot.Usb.Windows.Win32API;

namespace FirmwareKit.Comm.Fastboot.Usb.Windows;

public class WinUSBFinder
{
    /// <summary>
    /// Common Android USB Driver Interface GUIDs. 
    /// Many modern Fastboot drivers use the Google one, 
    /// while others might use older or generic WinUSB GUIDs.
    /// </summary>
    public static readonly GUID[] KnowIntPtrerfaceGUIDs =
    {
    new GUID { Data1 = 0xf72fe0d4, Data2 = 0xcbcb, Data3 = 0x407d, Data4 = [0x88, 0x14, 0x9e, 0xd6, 0x73, 0xd0, 0xdd, 0x6b] },
    new GUID { Data1 = 0x78a1c341, Data2 = 0x4539, Data3 = 0x11d3, Data4 = [0xb8, 0x8d, 0x00, 0xc0, 0x4f, 0xad, 0x51, 0x71] },
    new GUID { Data1 = 0xcae59032, Data2 = 0x0402, Data3 = 0x4a73, Data4 = [0x9f, 0x9d, 0x2a, 0x1b, 0x1f, 0x34, 0xd9, 0x76] },
    new GUID { Data1 = 0xdee82443, Data2 = 0x3064, Data3 = 0x4401, Data4 = [0x92, 0x3f, 0x56, 0x45, 0x36, 0x29, 0xe4, 0x14] },
    new GUID { Data1 = 0xa5dcbf10, Data2 = 0x6530, Data3 = 0x11d2, Data4 = [0x90, 0x1f, 0x00, 0xc0, 0x4f, 0xb9, 0x51, 0xed] }
};

    /// <summary>
    /// List of known Fastboot Vendor IDs for matching when discovering devices.
    /// </summary>
    public static readonly ushort[] KnownVendorIds =
    {
    0x18d1,
    0x0451,
    0x0502,
    0x0fce,
    0x05c6,
    0x22b8,
    0x0955,
    0x413c,
    0x0bb4,
    0x1921,
    0x2717,
    0x12d1,
    0x04e8,
    0x0502,
    0x0b05,
};

    public static readonly uint IoGetDescriptorCode = ((FILE_DEVICE_UNKNOWN) << 16) | ((FILE_READ_ACCESS) << 14) | ((10) << 2) | (METHOD_BUFFERED);

    public static List<UsbDevice> FindDevice()
    {
        List<UsbDevice> devices = new List<UsbDevice>();
        HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var guid in KnowIntPtrerfaceGUIDs)
        {
            GUID currentGuid = guid;
            IntPtr devInfo = SetupDiGetClassDevsW(ref currentGuid, null, 0, DIGCF_DEVICEINTERFACE | DIGCF_PRESENT);
            if (devInfo.ToInt64() == -1)
                continue;

            try
            {
                uint index;
                for (index = 0; ; index++)
                {
                    SpDeviceInterfaceData interfaceData = new SpDeviceInterfaceData();
                    interfaceData.cbSize = (uint)Marshal.SizeOf<SpDeviceInterfaceData>();
                    if (SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref currentGuid, index, ref interfaceData))
                    {
                        uint sizeResult = GetInterfaceDetailDataRequiredSize(devInfo, interfaceData);
                        IntPtr buffer = Marshal.AllocHGlobal((int)sizeResult);
                        Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetailW(devInfo, ref interfaceData,
                            buffer, sizeResult, out _, IntPtr.Zero))
                        {
                            Marshal.FreeHGlobal(buffer);
                            continue;
                        }
                        else
                        {
                            string? devicePath = Marshal.PtrToStringUni(buffer + 4);
                            Marshal.FreeHGlobal(buffer);
                            if (string.IsNullOrEmpty(devicePath) || seenPaths.Contains(devicePath))
                                continue;

                            seenPaths.Add(devicePath);
                            var (vid, pid) = ParseVidPid(devicePath);

                            WinUSBDevice winUsb = new WinUSBDevice { DevicePath = devicePath, VendorId = vid, ProductId = pid, UsbDeviceType = UsbDeviceType.WinUSB };
                            int winUsbResult = winUsb.CreateHandle();
                            if (winUsbResult == 0)
                            {
                                devices.Add(winUsb);
                            }
                            else
                            {
                                winUsb.Dispose();
                                if (winUsbResult == -1)
                                    continue;
                                if (winUsbResult == 1)
                                    continue;

                                LegacyUsbDevice legacy = new LegacyUsbDevice { DevicePath = devicePath, VendorId = vid, ProductId = pid, UsbDeviceType = UsbDeviceType.WinLegacy };
                                if (legacy.CreateHandle() == 0)
                                {
                                    devices.Add(legacy);
                                }
                                else
                                {
                                    legacy.Dispose();
                                }
                            }
                        }
                    }
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == ERROR_NO_MORE_ITEMS) break;
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }
        }
        return devices;
    }

    private static (ushort vid, ushort pid) ParseVidPid(string path)
    {
        var match = Regex.Match(path, @"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return (Convert.ToUInt16(match.Groups[1].Value, 16), Convert.ToUInt16(match.Groups[2].Value, 16));
        }
        return (0, 0);
    }

    private static uint GetInterfaceDetailDataRequiredSize(IntPtr devInfo, SpDeviceInterfaceData interfaceData)
    {
        uint requiredSize;
        if (!SetupDiGetDeviceInterfaceDetailW(devInfo, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ERROR_INSUFFICIENT_BUFFER)
                return requiredSize;
            throw new Win32Exception(error);
        }
        throw new Win32Exception(ERROR_INSUFFICIENT_BUFFER);
    }

    private static bool? isLegacyDevice(string devicePath)
    {
        byte[] data = new byte[32];
        int bytes_get;
        IntPtr hUsb = SimpleCreateHandle(devicePath);
        if (hUsb == new IntPtr(-1))
            return null;
        bool ret = DeviceIoControl(hUsb, IoGetDescriptorCode, Array.Empty<byte>(), 0, data, 32, out bytes_get, IntPtr.Zero);
        CloseHandle(hUsb);
        return ret;
    }


}
