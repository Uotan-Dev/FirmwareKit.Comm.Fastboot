using FirmwareKit.Comm.Usb.Abstractions;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Fastboot.Usb;

public sealed class CommUsbDevice : UsbDevice
{
    private const int DefaultIoTimeoutMs = 30000;
    private readonly global::FirmwareKit.Comm.IFirmwareKitComm _comm;
    private readonly UsbDeviceInfo _deviceInfo;
    private readonly bool _forceLibUsb;
    private IUsbDeviceSession? _session;

    public CommUsbDevice(global::FirmwareKit.Comm.IFirmwareKitComm comm, UsbDeviceInfo deviceInfo, bool forceLibUsb)
    {
        _comm = comm ?? throw new ArgumentNullException(nameof(comm));
        _deviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
        _forceLibUsb = forceLibUsb;

        DevicePath = _deviceInfo.DevicePath ?? string.Empty;
        SerialNumber = _deviceInfo.SerialNumber;
        VendorId = _deviceInfo.VendorId;
        ProductId = _deviceInfo.ProductId;
        UsbDeviceType = ToUsbDeviceType(_deviceInfo.SourceApiKind);
    }

    public override int CreateHandle()
    {
        if (_session != null)
        {
            return 0;
        }

        var filter = new UsbDeviceFilter
        {
            VendorId = _deviceInfo.VendorId,
            ProductId = _deviceInfo.ProductId,
            SerialNumber = _deviceInfo.SerialNumber,
            DevicePathContains = string.IsNullOrWhiteSpace(_deviceInfo.DevicePath) ? null : _deviceInfo.DevicePath,
            InterfaceClass = _deviceInfo.InterfaceClass ?? 0xFF,
            InterfaceSubClass = _deviceInfo.InterfaceSubClass ?? 0x42,
            InterfaceProtocol = _deviceInfo.InterfaceProtocol ?? 0x03,
        };

        _session = _comm.OpenUsbDeviceSession(ResolveApiKind(), filter);
        if (_session == null)
        {
            return -1;
        }

        SerialNumber = _session.DeviceInfo.SerialNumber;
        return 0;
    }

    public override byte[] Read(int length)
    {
        EnsureSession();
        if (length <= 0) return Array.Empty<byte>();
        return _session!.Read(length, DefaultIoTimeoutMs);
    }

    public override int ReadInto(byte[] buffer, int offset, int length)
    {
        EnsureSession();
        if (length <= 0) return 0;
        if (offset < 0 || length < 0 || offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        return _session!.ReadInto(buffer, offset, length, DefaultIoTimeoutMs);
    }

    public override long Write(byte[] data, int length)
    {
        EnsureSession();
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (length < 0 || length > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        return _session!.Write(data, length, DefaultIoTimeoutMs);
    }

    public override int GetSerialNumber()
    {
        EnsureSession();
        SerialNumber = _session!.DeviceInfo.SerialNumber;
        return string.IsNullOrEmpty(SerialNumber) ? -1 : 0;
    }

    public override void Reset()
    {
        if (_session == null) return;
        _session.Reset();
    }

    public override void Dispose()
    {
        _session?.Dispose();
        _session = null;
        GC.SuppressFinalize(this);
    }

    private void EnsureSession()
    {
        if (_session == null && CreateHandle() != 0)
        {
            throw new InvalidOperationException("Unable to open USB session through FirmwareKit.Comm.");
        }
    }

    private UsbApiKind ResolveApiKind()
    {
        if (_forceLibUsb)
        {
            return UsbApiKind.LibUsbDotNet;
        }

        if (_deviceInfo.SourceApiKind == UsbApiKind.LibUsbDotNet)
        {
            return UsbApiKind.LibUsbDotNet;
        }

        return UsbApiKind.Native;
    }

    private static UsbDeviceType ToUsbDeviceType(UsbApiKind apiKind)
    {
        if (apiKind == UsbApiKind.LibUsbDotNet)
        {
            return UsbDeviceType.LibUSB;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return UsbDeviceType.WinUSB;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return UsbDeviceType.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return UsbDeviceType.MacOS;
        }

        return UsbDeviceType.LibUSB;
    }
}
