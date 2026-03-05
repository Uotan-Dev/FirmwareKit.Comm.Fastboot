using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Fastboot.Backend.Usb.Windows;

public class Win32API
{
    //File
    public static uint GENERIC_READ { get; } = 0x80000000;
    public static uint GENERIC_WRITE { get; } = 0x40000000;
    public static uint FILE_SHARE_READ { get; } = 0x00000001;
    public static uint FILE_SHARE_WRITE { get; } = 0x00000002;
    public static uint OPEN_EXISTING { get; } = 3;
    public static IntPtr INVALID_HANDLE_VALUE { get; } = new IntPtr(-1);
    public static uint FILE_DEVICE_UNKNOWN { get; } = 0x00000022;
    public static uint METHOD_BUFFERED { get; } = 0;
    public static uint FILE_READ_ACCESS { get; } = 1;
    public static uint FILE_FLAG_OVERLAPPED { get; } = 0x40000000;

    public static uint CTL_CODE(uint deviceType, uint function, uint method, uint access)
    {
        return (deviceType << 16) | (access << 14) | (function << 2) | method;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern IntPtr CreateFileW([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint access,
                                         uint shareMode, IntPtr securityAttributes,
                                         uint createDisposition, uint flagsAndAttributes,
                                         IntPtr template);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern bool DeviceIoControl(IntPtr device, uint code,
                                              byte[]? inBuffer, int inBufferSize,
                                              byte[]? outBuffer, int outBufferSize,
                                              out int bytesReturned, IntPtr overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern bool WriteFile(IntPtr hFile, byte[] buffer, uint sizeToWrite, out uint bytesWritten, IntPtr overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern bool ReadFile(IntPtr hFile, byte[] buffer, uint sizeToRead, out uint bytesRead, IntPtr overlapped);

    //USB
    public struct GUID
    {
        public uint Data1;
        public ushort Data2;
        public ushort Data3;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Data4;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SpDeviceInterfaceData
    {
        public uint cbSize;
        public GUID InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct USBDeviceDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public ushort bcdUSB;
        public byte bDeviceClass;
        public byte bDeviceSubClass;
        public byte bDeviceProtocol;
        public byte bMaxPacketSize0;
        public ushort idVendor;
        public ushort idProduct;
        public ushort bcdDevice;
        public byte iManufacturer;
        public byte iProduct;
        public byte iSerialNumber;
        public byte bNumConfigurations;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct USBDeviceConfigDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public ushort wTotalLength;
        public byte bNumInterfaces;
        public byte bConfigurationValue;
        public byte iConfiguration;
        public byte bmAttributes;
        public byte MaxPower;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct USBDeviceInterfaceDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public byte bInterfaceNumber;
        public byte bAlternateSetting;
        public byte bNumEndpoints;
        public byte bInterfaceClass;
        public byte bInterfaceSubClass;
        public byte bInterfaceProtocol;
        public byte iInterface;
    }

    public static uint DIGCF_PRESENT { get; } = 0x00000002;
    public static uint DIGCF_DEVICEINTERFACE { get; } = 0x00000010;

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevsW(ref GUID guid, string? enumerator, int parent, uint flag);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData,
                                                          ref GUID interfaceClassGuid, uint index,
                                                          ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData,
                                                               IntPtr deviceInterfaceDetailData,
                                                               uint detailSize,
                                                               out uint requiredSize,
                                                               IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    public static IntPtr SimpleCreateHandle(string filePath, bool overlapped = false)
    {
        return CreateFileW(filePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero, OPEN_EXISTING,
            overlapped ? FILE_FLAG_OVERLAPPED : 0, IntPtr.Zero);
    }

    //ErrorCode
    public static int ERROR_INSUFFICIENT_BUFFER { get; } = 122;
    public static int ERROR_NO_MORE_ITEMS { get; } = 259;


}



