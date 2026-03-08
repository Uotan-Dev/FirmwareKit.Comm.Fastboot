using System.ComponentModel;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Fastboot.Usb.Windows.Win32API;

namespace FirmwareKit.Comm.Fastboot.Usb.Windows;

public class LegacyUsbDevice : UsbDevice
{
    private const int IoTimeoutMs = 30000;
    public static uint IoGetSerialCode => CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED, FILE_READ_ACCESS);
    public static uint IoGetDescriptorCode => CTL_CODE(FILE_DEVICE_UNKNOWN, 0x802, METHOD_BUFFERED, FILE_READ_ACCESS);

    private IntPtr fileHandle = INVALID_HANDLE_VALUE;

    public IntPtr Handle => fileHandle;

    public override int CreateHandle()
    {
        fileHandle = SimpleCreateHandle(DevicePath);
        if (fileHandle == INVALID_HANDLE_VALUE)
            return Marshal.GetLastWin32Error();

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
        var readTask = Task.Run(() =>
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
        });

        if (!readTask.Wait(IoTimeoutMs))
        {
            throw new TimeoutException($"Legacy USB read timed out after {IoTimeoutMs} ms.");
        }

        return readTask.GetAwaiter().GetResult();
    }

    public override long Write(byte[] data, int length)
    {
        var writeTask = Task.Run(() =>
        {
            uint written;
            if (WriteFile(fileHandle, data, (uint)length, out written, IntPtr.Zero))
            {
                return (long)written;
            }
            throw new Win32Exception(Marshal.GetLastWin32Error());
        });

        if (!writeTask.Wait(IoTimeoutMs))
        {
            throw new TimeoutException($"Legacy USB write timed out after {IoTimeoutMs} ms.");
        }

        return writeTask.GetAwaiter().GetResult();
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



