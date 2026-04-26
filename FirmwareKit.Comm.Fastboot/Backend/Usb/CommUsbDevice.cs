using FirmwareKit.Comm.Usb.Abstractions;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Fastboot.Usb;

/// <summary>
/// Concrete USB fastboot device implementation using FirmwareKit.Comm for USB communication.
/// <para>使用 FirmwareKit.Comm 进行 USB 通信的具体 USB fastboot 设备实现。</para>
/// </summary>
public sealed class CommUsbDevice : UsbDevice
{
    private const int DefaultIoTimeoutMs = 30000;
    private readonly global::FirmwareKit.Comm.IFirmwareKitComm _comm;
    private readonly UsbDeviceInfo _deviceInfo;
    private readonly bool _forceLibUsb;
    private IUsbDeviceSession? _session;

    /// <summary>
    /// Initializes a new CommUsbDevice with the specified communication interface, device info, and libusb preference.
    /// <para>使用指定的通信接口、设备信息和 libusb 偏好初始化新的 CommUsbDevice。</para>
    /// </summary>
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

    /// <summary>
    /// Creates a handle to the USB device. Returns 0 on success, -1 on failure.
    /// <para>创建 USB 设备句柄。成功返回 0，失败返回 -1。</para>
    /// </summary>
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

    /// <summary>
    /// Reads data from the USB device with the specified maximum length.
    /// <para>从 USB 设备读取指定最大长度的数据。</para>
    /// </summary>
    public override byte[] Read(int length)
    {
        EnsureSession();
        if (length <= 0) return Array.Empty<byte>();
        return _session!.Read(length, DefaultIoTimeoutMs);
    }

    /// <summary>
    /// Reads data directly into the specified buffer for zero-allocation reads.
    /// <para>将数据直接读入指定缓冲区，实现零分配读取。</para>
    /// </summary>
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

    /// <summary>
    /// Writes data to the USB device, returning the number of bytes written.
    /// <para>向 USB 设备写入数据，返回写入的字节数。</para>
    /// </summary>
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

    /// <summary>
    /// Retrieves the serial number of the USB device. Returns 0 on success, -1 on failure.
    /// <para>获取 USB 设备的序列号。成功返回 0，失败返回 -1。</para>
    /// </summary>
    public override int GetSerialNumber()
    {
        EnsureSession();
        SerialNumber = _session!.DeviceInfo.SerialNumber;
        return string.IsNullOrEmpty(SerialNumber) ? -1 : 0;
    }

    /// <summary>
    /// Resets the USB device connection.
    /// <para>重置 USB 设备连接。</para>
    /// </summary>
    public override void Reset()
    {
        if (_session == null) return;
        _session.Reset();
    }

    /// <summary>
    /// Releases the USB device session and all associated resources.
    /// <para>释放 USB 设备会话及所有关联资源。</para>
    /// </summary>
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
