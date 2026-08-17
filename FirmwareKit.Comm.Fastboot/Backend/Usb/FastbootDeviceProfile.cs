namespace FirmwareKit.Comm.Fastboot.Usb;

/// <summary>
/// Specifies how USB fastboot devices are matched during enumeration.
/// <para>指定枚举时 USB fastboot 设备的匹配方式。</para>
/// </summary>
public enum UsbMatchMode
{
    /// <summary>
    /// Only match devices exposing the standard fastboot interface (class 0xff, subclass 0x42, protocol 0x03).
    /// This is the default and matches AOSP behavior.
    /// <para>仅匹配暴露标准 fastboot 接口（class 0xff、subclass 0x42、protocol 0x03）的设备。
    /// 这是默认行为，与 AOSP 一致。</para>
    /// </summary>
    InterfaceOnly = 0,

    /// <summary>
    /// Match the standard fastboot interface first; additionally include devices listed in
    /// <see cref="UsbManager.KnownDeviceProfiles"/> by vendor/product id, even when their
    /// interface descriptor is not the standard fastboot one (e.g. legacy Kindle, some OEM bootloaders).
    /// <para>优先匹配标准 fastboot 接口；此外，将 <see cref="UsbManager.KnownDeviceProfiles"/>
    /// 中按厂商/产品 ID 列出的设备也纳入发现范围，即使其接口描述符并非标准 fastboot
    /// 接口（例如老款 Kindle、部分厂商 bootloader）。</para>
    /// </summary>
    InterfaceOrKnownVidPid = 1
}

/// <summary>
/// Describes a known fastboot-capable device identified by vendor/product id,
/// used as a discovery fallback for devices with non-standard interface descriptors.
/// <para>描述一个已知支持 fastboot 协议、以厂商/产品 ID 标识的设备，
/// 用于兜底发现接口描述符非标准的设备。</para>
/// </summary>
public sealed class FastbootDeviceProfile
{
    /// <summary>
    /// Gets the USB vendor id (VID).
    /// <para>获取 USB 厂商 ID（VID）。</para>
    /// </summary>
    public ushort VendorId { get; }

    /// <summary>
    /// Gets the USB product id (PID), or null to match any product id under the vendor.
    /// <para>获取 USB 产品 ID（PID），为 null 表示匹配该厂商下的任意产品 ID。</para>
    /// </summary>
    public ushort? ProductId { get; }

    /// <summary>
    /// Gets a human-readable name for the device family.
    /// <para>获取设备系列的易读名称。</para>
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new fastboot device profile.
    /// <para>初始化一个新的 fastboot 设备档案。</para>
    /// </summary>
    public FastbootDeviceProfile(ushort vendorId, ushort? productId, string name)
    {
        VendorId = vendorId;
        ProductId = productId;
        Name = name;
    }

    /// <summary>
    /// Determines whether the given vendor/product ids match this profile.
    /// <para>判断给定的厂商/产品 ID 是否匹配该档案。</para>
    /// </summary>
    public bool Matches(ushort vendorId, ushort productId)
        => VendorId == vendorId && (ProductId is null || ProductId.Value == productId);
}
