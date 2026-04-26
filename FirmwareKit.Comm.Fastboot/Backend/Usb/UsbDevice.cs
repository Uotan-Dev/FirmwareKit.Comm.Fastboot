

namespace FirmwareKit.Comm.Fastboot.Usb;

/// <summary>
/// Abstract base class for USB fastboot devices, implementing the buffered transport interface.
/// <para>USB fastboot 设备的抽象基类，实现缓冲传输接口。</para>
/// </summary>
public abstract class UsbDevice : IFastbootBufferedTransport
{
    /// <summary>
    /// Gets or sets the device path identifier.
    /// <para>获取或设置设备路径标识符。</para>
    /// </summary>
    public string DevicePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device serial number.
    /// <para>获取或设置设备序列号。</para>
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Gets or sets the USB vendor ID.
    /// <para>获取或设置 USB 厂商 ID。</para>
    /// </summary>
    public ushort VendorId { get; set; }

    /// <summary>
    /// Gets or sets the USB product ID.
    /// <para>获取或设置 USB 产品 ID。</para>
    /// </summary>
    public ushort ProductId { get; set; }

    /// <summary>
    /// Gets or sets the USB device type (platform-specific).
    /// <para>获取或设置 USB 设备类型（平台特定）。</para>
    /// </summary>
    public UsbDeviceType UsbDeviceType { get; set; }

    /// <summary>
    /// Reads data from the USB device with the specified maximum length.
    /// <para>从 USB 设备读取指定最大长度的数据。</para>
    /// </summary>
    public abstract byte[] Read(int length);

    /// <summary>
    /// Reads data directly into the specified buffer. Default implementation delegates to Read().
    /// <para>将数据直接读入指定缓冲区。默认实现委托给 Read()。</para>
    /// </summary>
    public virtual int ReadInto(byte[] buffer, int offset, int length)
    {
        if (length <= 0) return 0;
        if (offset < 0 || length < 0 || offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        byte[] data = Read(length);
        if (data.Length == 0) return 0;
        Buffer.BlockCopy(data, 0, buffer, offset, data.Length);
        return data.Length;
    }
    /// <summary>
    /// Writes data to the USB device, returning the number of bytes written.
    /// <para>向 USB 设备写入数据，返回写入的字节数。</para>
    /// </summary>
    public abstract long Write(byte[] data, int length);

    /// <summary>
    /// Retrieves the serial number of the USB device.
    /// <para>获取 USB 设备的序列号。</para>
    /// </summary>
    public abstract int GetSerialNumber();

    /// <summary>
    /// Creates a handle to the USB device for communication.
    /// <para>创建用于通信的 USB 设备句柄。</para>
    /// </summary>
    public abstract int CreateHandle();

    /// <summary>
    /// Resets the USB device connection.
    /// <para>重置 USB 设备连接。</para>
    /// </summary>
    public abstract void Reset();

    /// <summary>
    /// Releases the USB device handle and all associated resources.
    /// <para>释放 USB 设备句柄及所有关联资源。</para>
    /// </summary>
    public abstract void Dispose();
}

/// <summary>
/// Specifies the USB device backend type.
/// <para>指定 USB 设备后端类型。</para>
/// </summary>
public enum UsbDeviceType
{
    WinLegacy = 0,
    WinUSB = 1,
    Linux = 2,
    LibUSB = 3,
    MacOS = 4


}



