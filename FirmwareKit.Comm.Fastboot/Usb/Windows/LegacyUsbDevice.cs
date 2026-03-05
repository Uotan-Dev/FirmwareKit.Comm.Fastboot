using System.ComponentModel;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Fastboot.Usb.Windows.Win32API;

namespace FirmwareKit.Comm.Fastboot.Usb.Windows
{
    public class LegacyUsbDevice : UsbDevice
    {
        public static uint IoGetSerialCode => CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED, FILE_READ_ACCESS);
        public static uint IoGetDescriptorCode => CTL_CODE(FILE_DEVICE_UNKNOWN, 0x802, METHOD_BUFFERED, FILE_READ_ACCESS);

        private IntPtr fileHandle = INVALID_HANDLE_VALUE;

        public IntPtr Handle => fileHandle;

        public override int CreateHandle()
        {
            fileHandle = SimpleCreateHandle(DevicePath);
            if (fileHandle == INVALID_HANDLE_VALUE)
                return Marshal.GetLastWin32Error();

            // 仿照谷歌原生逻辑，检查是否匹配 Fastboot 接口标准 (0xff, 0x42, 0x03)
            // 在 Legacy 驱动中，我们尝试探测其是否响应特定的 IOCTL 
            if (!CheckInterface())
            {
                CloseHandle(fileHandle);
                fileHandle = INVALID_HANDLE_VALUE;
                return -1;
            }

            GetSerialNumber();
            return 0;
        }

        private bool CheckInterface()
        {
            byte[] buffer = new byte[256];
            int returned;
            // 能够响应 IoGetSerialCode，初步认为是兼容的 Legacy 驱动
            return DeviceIoControl(fileHandle, IoGetSerialCode, null, 0, buffer, buffer.Length, out returned, IntPtr.Zero);
        }

        public override int GetSerialNumber()
        {
            byte[] buffer = new byte[256];
            int returned;
            if (DeviceIoControl(fileHandle, IoGetSerialCode, null, 0, buffer, buffer.Length, out returned, IntPtr.Zero))
            {
                SerialNumber = System.Text.Encoding.Unicode.GetString(buffer, 0, returned).TrimEnd('\0');
                return 0;
            }
            return Marshal.GetLastWin32Error();
        }

        public override byte[] Read(int length)
        {
            byte[] buffer = new byte[length];
            uint read;
            if (ReadFile(fileHandle, buffer, (uint)length, out read, IntPtr.Zero))
            {
                byte[] result = new byte[read];
                Array.Copy(buffer, result, (int)read);
                return result;
            }
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public override long Write(byte[] data, int length)
        {
            uint written;
            if (WriteFile(fileHandle, data, (uint)length, out written, IntPtr.Zero))
            {
                return written;
            }
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public override void Reset()
        {
            // Legacy 驱动通常不支持软重置
        }

        public override void Dispose()
        {
            if (fileHandle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(fileHandle);
                fileHandle = INVALID_HANDLE_VALUE;
            }
            GC.SuppressFinalize(this);
        }
    }
}
