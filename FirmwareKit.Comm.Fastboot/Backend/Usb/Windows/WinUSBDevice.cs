using System.ComponentModel;
using System.Runtime.InteropServices;
using static FirmwareKit.Comm.Fastboot.Usb.Windows.WinUSBAPI;
using static FirmwareKit.Comm.Fastboot.Usb.Windows.Win32API;

namespace FirmwareKit.Comm.Fastboot.Usb.Windows;

public class WinUSBDevice : UsbDevice
{
    private byte InterfaceNum;
    private byte ReadBulkID, WriteBulkID;
    private byte ReadBulkIndex, WriteBulkIndex;
    private IntPtr WinUSBHandle;
    private IntPtr FileHandle;
    private Win32API.USBDeviceDescriptor USBDeviceDescriptor;
    private Win32API.USBDeviceConfigDescriptor USBDeviceConfigDescriptor;
    private Win32API.USBDeviceInterfaceDescriptor USBDeviceInterfaceDescriptor;

    public override int CreateHandle()
    {
        IntPtr hUsb = SimpleCreateHandle(DevicePath, true);
        uint bytesTransfered;
        if (hUsb == new IntPtr(-1))
            return Marshal.GetLastWin32Error();
        FileHandle = hUsb;
        if (!WinUsb_Initialize(hUsb, out WinUSBHandle))
            return Marshal.GetLastWin32Error();
        if (!WinUsb_GetCurrentAlternateSetting(WinUSBHandle, out InterfaceNum))
            return Marshal.GetLastWin32Error();
        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<USBDeviceDescriptor>());
        if (!WinUsb_GetDescriptor(WinUSBHandle, USB_DEVICE_DESCRIPTOR_TYPE, 0, 0, ptr, (uint)Marshal.SizeOf<USBDeviceDescriptor>(), out bytesTransfered))
            return Marshal.GetLastWin32Error();
        USBDeviceDescriptor = Marshal.PtrToStructure<USBDeviceDescriptor>(ptr);
        Marshal.FreeHGlobal(ptr);
        ptr = Marshal.AllocHGlobal(Marshal.SizeOf<USBDeviceConfigDescriptor>());
        if (!WinUsb_GetDescriptor(WinUSBHandle, USB_CONFIGURATION_DESCRIPTOR_TYPE, 0, 0, ptr, (uint)Marshal.SizeOf<USBDeviceConfigDescriptor>(), out bytesTransfered))
            return Marshal.GetLastWin32Error();
        USBDeviceConfigDescriptor = Marshal.PtrToStructure<USBDeviceConfigDescriptor>(ptr);
        Marshal.FreeHGlobal(ptr);
        if (!WinUsb_QueryInterfaceSettings(WinUSBHandle, InterfaceNum, out USBDeviceInterfaceDescriptor))
            return Marshal.GetLastWin32Error();

        if (USBDeviceInterfaceDescriptor.bInterfaceClass != 0xFF ||
            USBDeviceInterfaceDescriptor.bInterfaceSubClass != 0x42 ||
            USBDeviceInterfaceDescriptor.bInterfaceProtocol != 0x03)
        {
            if (USBDeviceDescriptor.idVendor != 0x18d1 || USBDeviceDescriptor.idProduct != 0xd00d)
            {
                return -1;
            }
        }

        for (byte endpoint = 0; endpoint < USBDeviceInterfaceDescriptor.bNumEndpoints; endpoint++)
        {
            WinUSBPipeInfo pipeInfo;
            if (!WinUsb_QueryPipe(WinUSBHandle, InterfaceNum, endpoint, out pipeInfo))
                return Marshal.GetLastWin32Error();
            if (pipeInfo.PipeType == WinUSBPipeType.UsbdPipeTypeBulk)
            {
                if ((pipeInfo.PipeID & USB_ENDPOINT_DIRECTION_MASK) != 0)
                {
                    if (ReadBulkID == 0)
                    {
                        ReadBulkID = pipeInfo.PipeID;
                        ReadBulkIndex = endpoint;
                    }
                }
                else
                {
                    if (WriteBulkID == 0)
                    {
                        WriteBulkID = pipeInfo.PipeID;
                        WriteBulkIndex = endpoint;
                    }
                }
            }
        }

        if (ReadBulkID == 0 || WriteBulkID == 0)
        {
            return -1;
        }

        GetSerialNumber();

        byte bTrue = 1;
        byte bFalse = 0;
        uint timeout = 60000; // Increased to 60s for large flash operations

        // Policy configuration
        WinUsb_SetPipePolicy(WinUSBHandle, ReadBulkID, AUTO_CLEAR_STALL, 1, ref bTrue);
        WinUsb_SetPipePolicy(WinUSBHandle, WriteBulkID, AUTO_CLEAR_STALL, 1, ref bTrue);
        WinUsb_SetPipePolicy(WinUSBHandle, ReadBulkID, PIPE_TRANSFER_TIMEOUT, 4, ref timeout);
        WinUsb_SetPipePolicy(WinUSBHandle, WriteBulkID, PIPE_TRANSFER_TIMEOUT, 4, ref timeout);

        // WinUSB RAW_IO can significantly improve stability for large transfers.
        // It requires that the transfer size is a multiple of the packet size (typically 512).
        WinUsb_SetPipePolicy(WinUSBHandle, ReadBulkID, RAW_IO, 1, ref bFalse);
        WinUsb_SetPipePolicy(WinUSBHandle, WriteBulkID, RAW_IO, 1, ref bFalse);

        // Align with AOSP host behavior: avoid forcing ZLP from the host side.
        WinUsb_SetPipePolicy(WinUSBHandle, WriteBulkID, SHORT_PACKET_TERMINATE, 1, ref bFalse);

        return 0;
    }

    public IntPtr Handle => WinUSBHandle != IntPtr.Zero ? WinUSBHandle : FileHandle;

    public override void Reset()
    {
        if (WinUSBHandle != IntPtr.Zero)
        {
            WinUsb_ResetPipe(WinUSBHandle, ReadBulkID);
            WinUsb_ResetPipe(WinUSBHandle, WriteBulkID);
        }
    }

    public override int GetSerialNumber()
    {
        uint bytes_get;
        uint descriptorSize = 64;
        IntPtr ptr = Marshal.AllocHGlobal((int)descriptorSize);
        while (!WinUsb_GetDescriptor(WinUSBHandle, USB_STRING_DESCRIPTOR_TYPE,
            USBDeviceDescriptor.iSerialNumber, 0x0409,
            ptr, descriptorSize, out bytes_get))
        {
            if ((uint)Marshal.GetLastWin32Error() != (uint)ERROR_INSUFFICIENT_BUFFER)
                return Marshal.GetLastWin32Error();
            descriptorSize *= 2;
            Marshal.FreeHGlobal(ptr);
            ptr = Marshal.AllocHGlobal((int)descriptorSize);
        }
        SerialNumber = Marshal.PtrToStringUni(ptr + 2, (int)(bytes_get - 2) / 2)?.TrimEnd('\0');
        Marshal.FreeHGlobal(ptr);
        return 0;
    }

    public override byte[] Read(int length)
    {
        if (WinUSBHandle == IntPtr.Zero) throw new Exception("Device handle is closed.");

        byte[] data = new byte[length];
        uint totalBytesRead = 0;

        // AOSP style: Read in a loop until requested length is met or a short packet is received.
        while (totalBytesRead < length)
        {
            uint toRead = (uint)Math.Min(length - totalBytesRead, 1024 * 1024);
            uint bytesRead;

            if (WinUsb_ReadPipe(WinUSBHandle, ReadBulkID, data, toRead, out bytesRead, IntPtr.Zero))
            {
                if (bytesRead == 0) break;
                totalBytesRead += bytesRead;

                // If we got a short packet (less than requested), it signifies end of transfer.
                if (bytesRead < toRead) break;
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                if (err == 121) break;
                throw new Win32Exception(err);
            }
        }

        if (totalBytesRead == length) return data;

        if (totalBytesRead == 0 && length > 0) return Array.Empty<byte>();

        byte[] realData = new byte[totalBytesRead];
        Array.Copy(data, realData, (int)totalBytesRead);
        return realData;
    }

    public override long Write(byte[] data, int length)
    {
        if (WinUSBHandle == IntPtr.Zero) throw new Exception("Device handle is closed.");

        if (length == 0)
        {
            return 0;
        }

        uint totalBytesWritten = 0;
        uint toWrite = (uint)length;
        uint bytesWritten;

        if (WinUsb_WritePipe(WinUSBHandle, WriteBulkID, data, toWrite, out bytesWritten, IntPtr.Zero))
        {
            totalBytesWritten = bytesWritten;

            // Critical Fix for ZLP (Zero Length Packet):
            // Some bootloaders (like QCOM) wait specifically for a Zero Length Packet
            // if the transfer size is a multiple of MaxPacketSize (512).
            // We intentionally keep SHORT_PACKET_TERMINATE disabled to mirror AOSP host behavior
            // and avoid forcing host-side ZLP from the transport layer.
        }
        else
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return (long)totalBytesWritten;
    }

    public override void Dispose()
    {
        if (WinUSBHandle != IntPtr.Zero)
        {
            WinUsb_Free(WinUSBHandle);
            WinUSBHandle = IntPtr.Zero;
        }
        if (FileHandle != IntPtr.Zero && FileHandle != new IntPtr(-1))
        {
            CloseHandle(FileHandle);
            FileHandle = IntPtr.Zero;
        }
    }


}



