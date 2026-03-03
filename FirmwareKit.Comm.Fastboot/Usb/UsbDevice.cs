namespace FirmwareKit.Comm.Fastboot.Usb;

public abstract class UsbDevice : IFastbootTransport
{
    public string DevicePath { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public ushort VendorId { get; set; }
    public ushort ProductId { get; set; }
    public UsbDeviceType UsbDeviceType { get; set; }
    public abstract byte[] Read(int length);
    public abstract long Write(byte[] data, int length);
    public abstract int GetSerialNumber();
    public abstract int CreateHandle();
    public abstract void Reset();
    public abstract void Dispose();
}

public enum UsbDeviceType
{
    WinLegacy = 0,
    WinUSB = 1,
    Linux = 2,
    LibUSB = 3,
    MacOS = 4


}