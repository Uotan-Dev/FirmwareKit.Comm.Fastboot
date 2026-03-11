using FirmwareKit.Comm.Fastboot.Network;
using FirmwareKit.Comm.Fastboot.Usb;
using FirmwareKit.Lp;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver : IDisposable
{
    public void Dispose()
    {
        FastbootDebug.Log($"Dispose()");
        Transport?.Dispose();
    }

    /// <summary>
    /// Resets the transport (clears pipes/buffers)
    /// </summary>
    public void ResetTransport()
    {
        FastbootDebug.Log($"ResetTransport()");
        if (Transport is UsbDevice usb)
        {
            usb.Reset();
        }
        else if (Transport is TcpTransport tcp)
        {
            // TCP transport might need its own reset logic if implemented
        }
    }

    /// <summary>
    /// Determines whether the device is in fastbootd (userspace) mode.
    /// </summary>
    public bool IsUserspace()
    {
        FastbootDebug.Log($"IsUserspace()");
        try
        {
            // Always query device directly; cached values can be stale across reboot mode changes.
            return GetVar("is-userspace", useCache: false, quiet: true) == "yes";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether a partition is logical. If super partition metadata is loaded, it is used with priority.
    /// </summary>
    public bool IsLogicalOptimized(string partition)
    {
        FastbootDebug.Log($"IsLogicalOptimized(partition={partition})");
        if (_logicalPartitionsFromMetadata != null)
        {
            return _logicalPartitionsFromMetadata.Contains(partition);
        }
        return IsLogical(partition);
    }

    /// <summary>
    /// Parses super_empty.img and extracts the logical partition list.
    /// </summary>
    public void LoadLogicalPartitionsFromMetadata(string superImagePath)
    {
        FastbootDebug.Log($"LoadLogicalPartitionsFromMetadata(superImagePath={superImagePath})");
        if (!File.Exists(superImagePath))
        {
            _logicalPartitionsFromMetadata = null;
            return;
        }
        try
        {
            var meta = ReadFromImageFile(superImagePath);
            var partitions = new HashSet<string>();
            foreach (var part in meta.Partitions)
            {
                var name = part.Name.ToString();
                if (!string.IsNullOrEmpty(name))
                    partitions.Add(name);
            }
            _logicalPartitionsFromMetadata = partitions;
        }
        catch
        {
            _logicalPartitionsFromMetadata = null;
        }
    }

    public static LpMetadata ReadFromImageFile(string path)
    {
        FastbootDebug.Log($"ReadFromImageFile(path={path})");
        using var stream = File.OpenRead(path);
        return ReadFromImageStream(stream);
    }

    public static LpMetadata ReadFromImageStream(Stream stream)
    {
        FastbootDebug.Log($"ReadFromImageStream(stream={stream})");
        long[] tryOffsets = [ MetadataFormat.LP_PARTITION_RESERVED_BYTES,
                      MetadataFormat.LP_PARTITION_RESERVED_BYTES + MetadataFormat.LP_METADATA_GEOMETRY_SIZE,
                      0 ];

        foreach (var offset in tryOffsets)
        {
            try
            {
                LpLogger.Info($"Trying to read geometry info from offset {offset}...");
                var buffer = new byte[MetadataFormat.LP_METADATA_GEOMETRY_SIZE];
                stream.Seek(offset, SeekOrigin.Begin);
                if (stream.Read(buffer, 0, buffer.Length) == buffer.Length)
                {
                    MetadataReader.ParseGeometry(buffer, out var geometry);
                    var metadataOffset = offset;
                    if (offset == MetadataFormat.LP_PARTITION_RESERVED_BYTES + MetadataFormat.LP_METADATA_GEOMETRY_SIZE)
                    {
                        metadataOffset -= MetadataFormat.LP_METADATA_GEOMETRY_SIZE;
                    }
                    stream.Seek(metadataOffset + (MetadataFormat.LP_METADATA_GEOMETRY_SIZE * 2), SeekOrigin.Begin);
                    var metadata = MetadataReader.ParseMetadata(geometry, stream);
                    LpLogger.Info($"Successfully parsed metadata: partitions={metadata.Partitions.Count}, groups={metadata.Groups.Count}");
                    return metadata;
                }
            }
            catch (Exception ex)
            {
                LpLogger.Warning($"Failed to parse at offset {offset}: {ex.Message}");
                continue;
            }
        }
        throw new InvalidDataException("Valid LpMetadataGeometry not found. The image may not be a super image or may be corrupted.");
    }
    public IFastbootTransport Transport { get; private set; }
    private Dictionary<string, string> _varCache = [];
    private Dictionary<string, bool> _hasSlotCache = [];
    private HashSet<string>? _logicalPartitionsFromMetadata = null;

    public FastbootDriver(IFastbootTransport transport) => Transport = transport;
    public static int ReadTimeoutSeconds = 30;
    /// <summary>
    /// Size of data to send in a single chunk (512KB for better WinUSB/Qualcomm compatibility)
    /// </summary>
    public static int OnceSendDataSize = 512 * 1024;
    // Host-side resparse limit. Keep this <= fastboot protocol DATA field max (uint32).
    // A wider default avoids unnecessary sparse conversion for large images, reducing peak memory usage.
    public static long SparseMaxDownloadSize = uint.MaxValue;

    private static readonly string[] PartitionPriority = {
    "preloader", "bootloader", "radio", "dram", "md1img", "xbl", "abl", "keystore",
    "boot", "dtbo", "init_boot", "vendor_boot", "pvmfw",
    "vbmeta", "vbmeta_system", "vbmeta_vendor", "vbmeta_custom",
    "recovery", "system", "vendor", "product", "system_ext", "odm", "vendor_dlkm", "odm_dlkm", "system_dlkm"
};

    public event EventHandler<FastbootReceivedFromDeviceEventArgs>? ReceivedFromDevice;
    public event EventHandler<(long, long)>? DataTransferProgressChanged;
    public event EventHandler<string>? CurrentStepChanged;

    public void NotifyCurrentStep(string step)
    {
        FastbootDebug.Log($"NotifyCurrentStep(step={step})");
        CurrentStepChanged?.Invoke(this, step);
    }
    public void NotifyProgress(long current, long total)
    {
        FastbootDebug.Log($"NotifyProgress(current={current}, total={total})");
        DataTransferProgressChanged?.Invoke(this, (current, total));
    }
    public void NotifyReceived(FastbootState state, string? info = null, string? text = null)
    {
        FastbootDebug.Log($"NotifyReceived(state={state}, info={info}, text={text})");
        ReceivedFromDevice?.Invoke(this, new FastbootReceivedFromDeviceEventArgs(state, info, text));
    }

    /// <summary>
    /// Checks if a partition has slots based on the "has-slot" variable from bootloader.
    /// Results are cached to minimize round-trips.
    /// </summary>
    public bool HasSlot(string partition)
    {
        FastbootDebug.Log($"HasSlot(partition={partition})");
        if (string.IsNullOrEmpty(partition)) return false;
        if (_hasSlotCache.TryGetValue(partition, out bool has)) return has;

        try
        {
            string val = GetVar("has-slot:" + partition);
            has = (val == "yes");
            _hasSlotCache[partition] = has;
            return has;
        }
        catch
        {
            _hasSlotCache[partition] = false;
            return false;
        }
    }

    /// <summary>
    /// Gets the current active slot (usually 'a' or 'b').
    /// </summary>
    public string GetCurrentSlot()
    {
        FastbootDebug.Log($"GetCurrentSlot()");
        try
        {
            string slot = GetVar("current-slot");
            if (slot.StartsWith("_")) slot = slot.Substring(1);
            return slot;
        }
        catch
        {
            return "";
        }
    }


    /// <param name="serial">Optional: specify the serial number</param>
    /// <param name="timeoutSeconds">The timeout duration (seconds), -1 means wait forever</param>
    public static FastbootDriver? WaitForDevice(Func<List<UsbDevice>> deviceFinder, string? serial = null, int timeoutSeconds = -1)
    {
        FastbootDebug.Log($"WaitForDevice(deviceFinder={deviceFinder}, serial={serial}, timeoutSeconds={timeoutSeconds})");
        DateTime start = DateTime.Now;
        while (timeoutSeconds == -1 || (DateTime.Now - start).TotalSeconds < timeoutSeconds)
        {
            var devices = deviceFinder();
            UsbDevice? found = null;
            if (string.IsNullOrEmpty(serial))
            {
                found = devices.FirstOrDefault();
            }
            else
            {
                found = devices.FirstOrDefault(d =>
                {
                    try { d.GetSerialNumber(); return d.SerialNumber == serial; }
                    catch { return false; }
                });
            }

            if (found != null)
            {
                foreach (var d in devices) if (d != found) d.Dispose();
                return new FastbootDriver(found);
            }

            foreach (var d in devices) d.Dispose();
        }
        return null;
    }
    /// <summary>
    /// Gets all attributes
    /// </summary>
    public Dictionary<string, string> GetVarAll()
    {
        FastbootDebug.Log($"GetVarAll()");
        _varCache.Clear();
        try
        {
            var res = RawCommand("getvar:all").ThrowIfError();
            FastbootDebug.Log("Command response received. Parsing...");
            var dict = new Dictionary<string, string>();
            foreach (var line in res.Info)
            {
                FastbootDebug.Log("Parsing line: " + line);
                int colonIdx = line.LastIndexOf(":");
                if (colonIdx > 0)
                {
                    string k = line.Substring(0, colonIdx).Trim();
                    string v = line.Substring(colonIdx + 1).TrimStart();
                    FastbootDebug.Log($"Parsed key: {k}, value: {v}");
                    if (!dict.ContainsKey(k))
                    {
                        dict[k] = v;
                        _varCache[k] = v;
                    }
                }
            }
            return dict;
        }
        catch (Exception ex)
        {
            FastbootDebug.Log("Exception in GetVarAll: " + ex);
            throw;
        }
    }

    /// <summary>
    /// Gets a single attribute (with caching if enabled)
    /// </summary>
    public string GetVar(string key, bool useCache = true, bool quiet = false)
    {
        FastbootDebug.Log($"GetVar(key={key}, useCache={useCache}, quiet={quiet})");
        if (useCache && _varCache.TryGetValue(key, out string? cached)) return cached;
        var resObj = RawCommand("getvar:" + key, quiet);
        if (resObj.Result == FastbootState.Fail || resObj.Result == FastbootState.Timeout)
        {
            // Do not cache transient failures/timeouts; caller can retry later.
            return "";
        }
        var res = resObj.Response;
        if (useCache) _varCache[key] = res;
        return res;
    }

    /// <summary>
    /// Gets the number of slots
    /// </summary>
    public int GetSlotCount()
    {
        FastbootDebug.Log($"GetSlotCount()");
        int slot_count = 1;
        string count = GetVar("slot-count");
        int.TryParse(count, out slot_count);
        return slot_count;
    }

    /// <summary>
    /// If there is an active virtual A/B snapshot, attempts to cancel it
    /// </summary>
    public void CancelSnapshotIfNeeded()
    {
        try
        {
            string status = GetVar("snapshot-update-status");
            if (!string.IsNullOrEmpty(status) && status != "none")
            {
                SnapshotUpdate("cancel");
            }
        }
        catch { }
    }

    /// <summary>
    /// Ensures the device is in fastbootd (userspace) mode, if not then automatically restarts
    /// </summary>
    public void EnsureUserspace()
    {
        if (!IsUserspace())
        {
            NotifyCurrentStep("Operation requires fastbootd, rebooting...");
            Reboot("fastboot").ThrowIfError();

            // Clear stale vars (including is-userspace=no) before reconnect and re-check.
            _varCache.Clear();

            // Match AOSP behavior: allow disconnect to happen before attempting reconnect.
            System.Threading.Thread.Sleep(1000);
            NotifyCurrentStep("waiting for any device >");

            if (Transport is UsbDevice usbDev)
            {
                var newUtil = WaitForDevice(UsbManager.GetAllDevices, usbDev.SerialNumber, 30);
                if (newUtil == null) throw new Exception("Failed to boot into userspace fastboot; one or more components might be unbootable.");

                this.Transport = newUtil.Transport;
            }
            else if (Transport is TcpTransport tcp)
            {
                string host = tcp.Host;
                int port = tcp.Port;
                tcp.Dispose();

                DateTime start = DateTime.Now;
                bool connected = false;
                while ((DateTime.Now - start).TotalSeconds < 60)
                {
                    try
                    {
                        Transport = new TcpTransport(host, port);
                        connected = true;
                        break;
                    }
                    catch { System.Threading.Thread.Sleep(1000); }
                }
                if (!connected) throw new Exception("Failed to boot into userspace fastboot; one or more components might be unbootable.");
            }
            else if (Transport is UdpTransport udp)
            {
                string host = udp.Host;
                int port = udp.Port;
                udp.Dispose();

                DateTime start = DateTime.Now;
                bool connected = false;
                while ((DateTime.Now - start).TotalSeconds < 60)
                {
                    try
                    {
                        Transport = new UdpTransport(host, port);
                        connected = true;
                        break;
                    }
                    catch { System.Threading.Thread.Sleep(1000); }
                }
                if (!connected) throw new Exception("Failed to boot into userspace fastboot; one or more components might be unbootable.");
            }
            else
            {
                throw new NotSupportedException("Automatic reboot to userspace is only supported for USB, TCP and UDP transports.");
            }

            DateTime userspaceWaitStart = DateTime.Now;
            bool enteredUserspace = false;
            while ((DateTime.Now - userspaceWaitStart).TotalSeconds < 30)
            {
                if (IsUserspace())
                {
                    enteredUserspace = true;
                    break;
                }

                System.Threading.Thread.Sleep(1000);
            }

            if (!enteredUserspace)
            {
                throw new Exception("Failed to boot into userspace fastboot; one or more components might be unbootable.");
            }

            _varCache.Clear();
        }
    }

    /// <summary>
    /// Validates Product Info (android-info.txt)
    /// </summary>
    public bool ValidateProductInfo(string content, out string? error)
    {
        var parser = new ProductInfoParser(this);
        return parser.Validate(content, out error);
    }

    /// <summary>
    /// Downloads and grabs staged data (staged data)
    /// </summary>
    public void GetStaged(string outputPath)
    {
        using var fs = File.Create(outputPath);
        UploadData("get_staged", fs);
    }

    /// <summary>
    /// Prints standard device information (bootloader version, baseband version, serial number, etc.)
    /// </summary>
    public void DumpInfo()
    {
        NotifyCurrentStep("--------------------------------------------");
        try { NotifyCurrentStep("Bootloader Version...: " + GetVar("version-bootloader")); } catch { }
        try { NotifyCurrentStep("Baseband Version.....: " + GetVar("version-baseband")); } catch { }
        try { NotifyCurrentStep("Serial Number........: " + GetVar("serialno")); } catch { }
        NotifyCurrentStep("--------------------------------------------");
    }

    /// <summary>
    /// Flashes ZIP firmware package (corresponding to fastboot update)
    /// </summary>
    public void FlashZip(string zipPath, bool skipValidation = false, bool wipe = false)
    {
        CancelSnapshotIfNeeded();
        DumpInfo();

        string tempDir = Path.Combine(Path.GetTempPath(), "FirmwareKit.Comm.Fastboot_Zip_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            NotifyCurrentStep($"Extracting ZIP: {Path.GetFileName(zipPath)}");
            ZipFile.ExtractToDirectory(zipPath, tempDir);

            // Reuse FlashAll logic, it will automatically handle fastboot-info.txt, optimization of super partition, and verification
            FlashAll(tempDir, wipe, false, skipValidation);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }

    public long GetMaxDownloadSize()
    {
        string? sizeStr = null;
        try { sizeStr = GetVar("max-download-size"); } catch { }
        if (string.IsNullOrEmpty(sizeStr)) return SparseMaxDownloadSize;

        long parsedSize;

        if (sizeStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(sizeStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out parsedSize))
            {
                return SparseMaxDownloadSize;
            }
        }
        else
        {
            if (!long.TryParse(sizeStr, out parsedSize))
            {
                return SparseMaxDownloadSize;
            }
        }

        if (parsedSize <= 0)
        {
            return SparseMaxDownloadSize;
        }

        // Keep within protocol limits and the configurable host-side resparse limit.
        return Math.Min(Math.Min(parsedSize, SparseMaxDownloadSize), uint.MaxValue);
    }

    /// <summary>
    /// Whether CRC is supported (AOSP sparse protocol extension)
    /// </summary>
    public bool HasCrc()
    {
        try { return GetVar("has-crc") == "yes"; } catch { return false; }
    }

    /// <summary>
    /// Checks if the partition exists
    /// </summary>
    public bool PartitionExists(string partition)
    {
        try
        {
            string res = GetPartitionSize(partition);
            return !string.IsNullOrEmpty(res) && res != "0" && res != "0x0";
        }
        catch { return false; }
    }

    /// <summary>
    /// Smart flashing image (based on magic number, automatically determines if it's sparse and handles A/B slots)
    /// </summary>
    public void FlashImage(string partition, string filePath, string? slotOverride = null)
    {
        FastbootDebug.Log($"FlashImage(partition={partition}, file={filePath}, slot={slotOverride ?? "null"})");
        if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);

        string targetPartition = partition;
        if (slotOverride == "all")
        {
            FlashImage(partition, filePath, "a");
            FlashImage(partition, filePath, "b");
            return;
        }

        if (HasSlot(partition))
        {
            targetPartition = partition + "_" + (slotOverride ?? GetCurrentSlot());
        }

        FastbootDebug.Log($"Target Partition: {targetPartition}");

        // AOSP Optimal Placement: For logical partitions, zero out existing size
        // ResizeLogicalPartition also handles ensuring userspace (fastbootd)
        if (IsLogicalOptimized(targetPartition))
        {
            try { ResizeLogicalPartition(targetPartition, 0); } catch { }
        }

        try
        {
            FileInfo fi = new FileInfo(filePath);
            using var fs = File.OpenRead(filePath);
            FlashUnsparseImage(targetPartition, fs, fi.Length).ThrowIfError();
        }
        catch (Exception ex)
        {
            if (FastbootDebug.IsEnabled) Console.Error.WriteLine("[DEBUG] FlashImage Failed: " + ex);
            throw;
        }
    }

    /// <summary>
    /// Waits for the virtual A/B merge to complete (corresponding to snapshot-update merge --wait)
    /// </summary>
    public void WaitForSnapshotMerge(int timeoutSeconds = 600)
    {
        DateTime start = DateTime.Now;
        while ((DateTime.Now - start).TotalSeconds < timeoutSeconds)
        {
            var res = GetVar("snapshot-update-status");
            if (res == "merging")
            {
                NotifyCurrentStep("Waiting for snapshot merge...");
                System.Threading.Thread.Sleep(2000);
                continue;
            }
            if (res == "none" || res == "completed") return;
            break;
        }
    }

    /// <summary>
    /// Flashes image from stream (Updated to remove sparse handling)
    /// </summary>
    public void FlashImage(string partition, Stream stream)
    {
        string targetPartition = partition;
        if (HasSlot(partition))
        {
            targetPartition = partition + "_" + GetCurrentSlot();
        }

        // AOSP Optimal Placement: For logical partitions, zero out existing size
        // ResizeLogicalPartition also handles ensuring userspace (fastbootd)
        if (IsLogicalOptimized(targetPartition))
        {
            try { ResizeLogicalPartition(targetPartition, 0); } catch { }
        }

        try
        {
            // Directly flash the image without sparse handling
            FlashUnsparseImage(targetPartition, stream, stream.Length);
        }
        catch (Exception ex)
        {
            FastbootDebug.Log($"[ERROR] FlashImage failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Determines if the partition is logical
    /// </summary>
    public bool IsLogical(string partition)
    {
        try { return GetVar("is-logical:" + partition) == "yes"; } catch { return false; }
    }

    /// <summary>
    /// Gets the storage space size of the partition
    /// </summary>
    public long GetPartitionSizeLong(string partition)
    {
        try
        {
            var res = GetVar("partition-size:" + partition);
            if (string.IsNullOrEmpty(res)) return 0;
            if (res.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Convert.ToInt64(res, 16);
            else
                return Convert.ToInt64(res);
        }
        catch { return 0; }
    }

    /// <summary>
    /// Gets the storage space size of the partition
    /// </summary>
    public string GetPartitionSize(string partition)
    {
        try { return GetVar("partition-size:" + partition); } catch { return ""; }
    }

    /// <summary>
    /// Gets the storage system type of the partition
    /// </summary>
    public string GetPartitionType(string partition)
    {
        try { return GetVar("partition-type:" + partition); } catch { return ""; }
    }

    /// <summary>
    /// Creates a local filesystem image and flashes it (simulating fastboot format command)
    /// </summary>
    public void FormatPartitionLocal(string partition, string fsType = "ext4", long size = 0)
    {
        if (size <= 0)
        {
            var res = GetVar("partition-size:" + partition);
            if (!string.IsNullOrEmpty(res))
            {
                if (res.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    size = Convert.ToInt64(res, 16);
                else
                    size = Convert.ToInt64(res);
            }
        }
        if (size <= 0) size = 1024 * 1024 * 32;

        string tmpFile = Path.GetTempFileName();
        try
        {
            if (fsType == "ext4") FileSystemUtil.CreateEmptyExt4(tmpFile, size);
            else if (fsType == "f2fs") FileSystemUtil.CreateEmptyF2fs(tmpFile, size);
            else throw new NotSupportedException("fs type not supported: " + fsType);

            FlashImage(partition, tmpFile);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    private byte[] CreateBootImageVersioned(byte[] kernel, byte[]? ramdisk, byte[]? second, byte[]? dtb, string? cmdline, string? name, uint version, uint base_addr, uint page_size)
    {
        switch (version)
        {
            case 0: return CreateBootImage(kernel, ramdisk, second, cmdline, name, base_addr, page_size);
            case 1: return CreateBootImage1(kernel, ramdisk, second, cmdline, name, base_addr, page_size);
            case 2: return CreateBootImage2(kernel, ramdisk, second, dtb, cmdline, name, base_addr, page_size);
            case 3: return CreateBootImage3(kernel, ramdisk, cmdline, 0); // OS version 0 as default
            case 4: return CreateBootImage4(kernel, ramdisk, cmdline, 0);
            case 5: return CreateBootImage5(kernel, ramdisk, cmdline, 0);
            case 6: return CreateBootImage6(kernel, ramdisk, cmdline, 0);
            default: throw new NotSupportedException($"Boot image header version {version} is not supported for dynamic packaging.");
        }
    }

    /// <summary>
    /// Generates BootImage data (V0 structure)
    /// </summary>
    public byte[] CreateBootImage(byte[] kernel, byte[]? ramdisk, byte[]? second, string? cmdline, string? name, uint base_addr, uint page_size)
    {
        BootImageHeaderV0 header = BootImageHeaderV0.Create();
        header.KernelSize = (uint)kernel.Length;
        header.KernelAddr = base_addr + 0x00008000;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.RamdiskAddr = base_addr + 0x01000000;
        header.SecondSize = (uint)(second?.Length ?? 0);
        header.SecondAddr = base_addr + 0x00F00000;
        header.TagsAddr = base_addr + 0x00000100;
        header.PageSize = page_size;

        if (!string.IsNullOrEmpty(cmdline))
        {
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdline);
            Array.Copy(cmdBytes, header.Cmdline, Math.Min(cmdBytes.Length, 512));
        }

        if (!string.IsNullOrEmpty(name))
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            Array.Copy(nameBytes, header.Name, Math.Min(nameBytes.Length, 16));
        }

        int headerSize = Marshal.SizeOf<BootImageHeaderV0>();
        int headerPages = (headerSize + (int)page_size - 1) / (int)page_size;
        int kernelPages = (kernel.Length + (int)page_size - 1) / (int)page_size;
        int ramdiskPages = ((ramdisk?.Length ?? 0) + (int)page_size - 1) / (int)page_size;
        int secondPages = ((second?.Length ?? 0) + (int)page_size - 1) / (int)page_size;

        int totalSize = (headerPages + kernelPages + ramdiskPages + secondPages) * (int)page_size;
        byte[] buffer = new byte[totalSize];

        byte[] headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(kernel, 0, buffer, headerPages * page_size, kernel.Length);
        if (ramdisk != null)
        {
            Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * page_size, ramdisk.Length);
        }
        if (second != null)
        {
            Array.Copy(second, 0, buffer, (headerPages + kernelPages + ramdiskPages) * page_size, second.Length);
        }

        return buffer;
    }

    /// <summary>
    /// Generates BootImage V1 data (containing Header Size)
    /// </summary>
    public byte[] CreateBootImage1(byte[] kernel, byte[]? ramdisk, byte[]? second, string? cmdline, string? name, uint base_addr, uint page_size)
    {
        BootImageHeaderV1 header = BootImageHeaderV1.Create();
        header.KernelSize = (uint)kernel.Length;
        header.KernelAddr = base_addr + 0x00008000;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.RamdiskAddr = base_addr + 0x01000000;
        header.SecondSize = (uint)(second?.Length ?? 0);
        header.SecondAddr = base_addr + 0x00F00000;
        header.TagsAddr = base_addr + 0x00000100;
        header.PageSize = page_size;
        header.HeaderSize = (uint)Marshal.SizeOf<BootImageHeaderV1>();

        if (!string.IsNullOrEmpty(cmdline))
        {
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdline);
            Array.Copy(cmdBytes, header.Cmdline, Math.Min(cmdBytes.Length, 512));
        }

        if (!string.IsNullOrEmpty(name))
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            Array.Copy(nameBytes, header.Name, Math.Min(nameBytes.Length, 16));
        }

        int headerPages = ((int)header.HeaderSize + (int)page_size - 1) / (int)page_size;
        int kernelPages = (kernel.Length + (int)page_size - 1) / (int)page_size;
        int ramdiskPages = ((ramdisk?.Length ?? 0) + (int)page_size - 1) / (int)page_size;
        int secondPages = ((second?.Length ?? 0) + (int)page_size - 1) / (int)page_size;

        int totalSize = (headerPages + kernelPages + ramdiskPages + secondPages) * (int)page_size;
        byte[] buffer = new byte[totalSize];

        byte[] headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(kernel, 0, buffer, headerPages * page_size, kernel.Length);
        if (ramdisk != null) Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * page_size, ramdisk.Length);
        if (second != null) Array.Copy(second, 0, buffer, (headerPages + kernelPages + ramdiskPages) * page_size, second.Length);

        return buffer;
    }

    /// <summary>
    /// Generates BootImage V2 data (containing DTB)
    /// </summary>
    public byte[] CreateBootImage2(byte[] kernel, byte[]? ramdisk, byte[]? second, byte[]? dtb, string? cmdline, string? name, uint base_addr, uint page_size)
    {
        BootImageHeaderV2 header = BootImageHeaderV2.Create();
        header.KernelSize = (uint)kernel.Length;
        header.KernelAddr = base_addr + 0x00008000;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.RamdiskAddr = base_addr + 0x01000000;
        header.SecondSize = (uint)(second?.Length ?? 0);
        header.SecondAddr = base_addr + 0x00F00000;
        header.TagsAddr = base_addr + 0x00000100;
        header.DtbSize = (uint)(dtb?.Length ?? 0);
        header.DtbAddr = (ulong)base_addr + 0x01100000;
        header.PageSize = page_size;
        header.HeaderSize = (uint)Marshal.SizeOf<BootImageHeaderV2>();

        if (!string.IsNullOrEmpty(cmdline))
        {
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdline);
            Array.Copy(cmdBytes, header.Cmdline, Math.Min(cmdBytes.Length, 512));
        }

        if (!string.IsNullOrEmpty(name))
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            Array.Copy(nameBytes, header.Name, Math.Min(nameBytes.Length, 16));
        }

        int headerPages = ((int)header.HeaderSize + (int)page_size - 1) / (int)page_size;
        int kernelPages = (kernel.Length + (int)page_size - 1) / (int)page_size;
        int ramdiskPages = ((ramdisk?.Length ?? 0) + (int)page_size - 1) / (int)page_size;
        int secondPages = ((second?.Length ?? 0) + (int)page_size - 1) / (int)page_size;
        int dtbPages = ((dtb?.Length ?? 0) + (int)page_size - 1) / (int)page_size;

        int totalSize = (headerPages + kernelPages + ramdiskPages + secondPages + dtbPages) * (int)page_size;
        byte[] buffer = new byte[totalSize];

        byte[] headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(kernel, 0, buffer, headerPages * page_size, kernel.Length);
        if (ramdisk != null) Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * page_size, ramdisk.Length);
        if (second != null) Array.Copy(second, 0, buffer, (headerPages + kernelPages + ramdiskPages) * page_size, second.Length);
        if (dtb != null) Array.Copy(dtb, 0, buffer, (headerPages + kernelPages + ramdiskPages + secondPages) * page_size, dtb.Length);

        return buffer;
    }

    /// <summary>
    /// Generates BootImage V3 data
    /// </summary>
    public byte[] CreateBootImage3(byte[] kernel, byte[]? ramdisk, string? cmdline, uint os_version)
    {
        BootImageHeaderV3 header = BootImageHeaderV3.Create();
        header.KernelSize = (uint)kernel.Length;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.OsVersion = os_version;
        header.HeaderSize = 4096;
        header.HeaderVersion = 3;

        if (!string.IsNullOrEmpty(cmdline))
        {
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdline);
            Array.Copy(cmdBytes, header.Cmdline, Math.Min(cmdBytes.Length, 1536));
        }

        const int page_size = 4096;
        int headerPages = (int)(header.HeaderSize + page_size - 1) / page_size;
        int kernelPages = (kernel.Length + page_size - 1) / page_size;
        int ramdiskPages = ((ramdisk?.Length ?? 0) + page_size - 1) / page_size;

        int totalSize = (headerPages + kernelPages + ramdiskPages) * page_size;
        byte[] buffer = new byte[totalSize];

        byte[] headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(kernel, 0, buffer, headerPages * page_size, kernel.Length);
        if (ramdisk != null)
            Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * page_size, ramdisk.Length);

        return buffer;
    }

    /// <summary>
    /// Generates BootImage V4 data (containing signature part)
    /// </summary>
    public byte[] CreateBootImage4(byte[] kernel, byte[]? ramdisk, string? cmdline, uint os_version, byte[]? signature = null)
    {
        BootImageHeaderV4 header = BootImageHeaderV4.Create();
        header.KernelSize = (uint)kernel.Length;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.OsVersion = os_version;
        header.HeaderSize = 4096;
        header.HeaderVersion = 4;
        header.SignatureSize = (uint)(signature?.Length ?? 0);

        if (!string.IsNullOrEmpty(cmdline))
        {
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdline);
            Array.Copy(cmdBytes, header.Cmdline, Math.Min(cmdBytes.Length, 1536));
        }

        const int page_size = 4096;
        int headerPages = (int)(header.HeaderSize + page_size - 1) / page_size;
        int kernelPages = (kernel.Length + page_size - 1) / page_size;
        int ramdiskPages = ((ramdisk?.Length ?? 0) + page_size - 1) / page_size;
        int sigPages = (int)((header.SignatureSize + page_size - 1) / page_size);

        int totalSize = (headerPages + kernelPages + ramdiskPages + sigPages) * page_size;
        byte[] buffer = new byte[totalSize];

        byte[] headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(kernel, 0, buffer, headerPages * page_size, kernel.Length);
        if (ramdisk != null)
            Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * page_size, ramdisk.Length);
        if (signature != null)
            Array.Copy(signature, 0, buffer, (headerPages + kernelPages + ramdiskPages) * page_size, signature.Length);

        return buffer;
    }

    /// <summary>
    /// Generates BootImage V5 data (containing Vendor Bootconfig)
    /// </summary>
    public byte[] CreateBootImage5(byte[] kernel, byte[]? ramdisk, string? cmdline, uint os_version, byte[]? signature = null, byte[]? bootconfig = null)
    {
        BootImageHeaderV5 header = BootImageHeaderV5.Create();
        header.KernelSize = (uint)kernel.Length;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.OsVersion = os_version;
        header.HeaderSize = 4096;
        header.HeaderVersion = 5;
        header.SignatureSize = (uint)(signature?.Length ?? 0);
        header.VendorBootconfigSize = (uint)(bootconfig?.Length ?? 0);

        if (!string.IsNullOrEmpty(cmdline))
        {
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdline);
            Array.Copy(cmdBytes, header.Cmdline, Math.Min(cmdBytes.Length, 1536));
        }

        const int page_size = 4096;
        int headerPages = (int)(header.HeaderSize + page_size - 1) / page_size;
        int kernelPages = (kernel.Length + page_size - 1) / page_size;
        int ramdiskPages = ((ramdisk?.Length ?? 0) + page_size - 1) / page_size;
        int sigPages = (int)((header.SignatureSize + page_size - 1) / page_size);
        int configPages = (int)((header.VendorBootconfigSize + page_size - 1) / page_size);

        int totalSize = (headerPages + kernelPages + ramdiskPages + sigPages + configPages) * page_size;
        byte[] buffer = new byte[totalSize];

        byte[] headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(kernel, 0, buffer, headerPages * page_size, kernel.Length);
        if (ramdisk != null)
            Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * page_size, ramdisk.Length);
        if (signature != null)
            Array.Copy(signature, 0, buffer, (headerPages + kernelPages + ramdiskPages) * page_size, signature.Length);
        if (bootconfig != null)
            Array.Copy(bootconfig, 0, buffer, (headerPages + kernelPages + ramdiskPages + sigPages) * page_size, bootconfig.Length);

        return buffer;
    }

    /// <summary>
    /// Generates BootImage V6 data (containing Extended Reserved Area)
    /// </summary>
    public byte[] CreateBootImage6(byte[] kernel, byte[]? ramdisk, string? cmdline, uint os_version, byte[]? signature = null, byte[]? bootconfig = null)
    {
        BootImageHeaderV6 header = BootImageHeaderV6.Create();
        header.KernelSize = (uint)kernel.Length;
        header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
        header.OsVersion = os_version;
        header.HeaderSize = 4096;
        header.HeaderVersion = 6;
        header.SignatureSize = (uint)(signature?.Length ?? 0);
        header.VendorBootconfigSize = (uint)(bootconfig?.Length ?? 0);

        if (!string.IsNullOrEmpty(cmdline))
        {
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdline);
            Array.Copy(cmdBytes, header.Cmdline, Math.Min(cmdBytes.Length, 1536));
        }

        const int page_size = 4096;
        int headerPages = (int)(header.HeaderSize + page_size - 1) / page_size;
        int kernelPages = (kernel.Length + page_size - 1) / page_size;
        int ramdiskPages = ((ramdisk?.Length ?? 0) + page_size - 1) / page_size;
        int sigPages = (int)((header.SignatureSize + page_size - 1) / page_size);
        int configPages = (int)((header.VendorBootconfigSize + page_size - 1) / page_size);

        int totalSize = (headerPages + kernelPages + ramdiskPages + sigPages + configPages) * page_size;
        byte[] buffer = new byte[totalSize];

        byte[] headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(kernel, 0, buffer, headerPages * page_size, kernel.Length);
        if (ramdisk != null)
            Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * page_size, ramdisk.Length);
        if (signature != null)
            Array.Copy(signature, 0, buffer, (headerPages + kernelPages + ramdiskPages) * page_size, signature.Length);
        if (bootconfig != null)
            Array.Copy(bootconfig, 0, buffer, (headerPages + kernelPages + ramdiskPages + sigPages) * page_size, bootconfig.Length);

        return buffer;
    }

    /// <summary>
    /// Generates Vendor Boot Image V3 data (containing DTB)
    /// </summary>
    public byte[] CreateVendorBootImage3(byte[] ramdisk, byte[] dtb, string? cmdline, string? product_name, uint page_size = 4096, uint base_addr = 0x10000000)
    {
        VendorBootImageHeaderV3 header = VendorBootImageHeaderV3.Create();
        header.PageSize = page_size;
        header.KernelAddr = base_addr + 0x00008000;
        header.RamdiskAddr = base_addr + 0x01000000;
        header.TagsAddr = base_addr + 0x00000100;
        header.VendorRamdiskSize = (uint)ramdisk.Length;
        header.DtbSize = (uint)dtb.Length;
        header.DtbAddr = (ulong)base_addr + 0x01100000;
        header.HeaderSize = (uint)Marshal.SizeOf<VendorBootImageHeaderV3>();

        if (!string.IsNullOrEmpty(cmdline))
        {
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdline);
            Array.Copy(cmdBytes, header.Cmdline, Math.Min(cmdBytes.Length, 2048));
        }

        if (!string.IsNullOrEmpty(product_name))
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(product_name);
            Array.Copy(nameBytes, header.Name, Math.Min(nameBytes.Length, 16));
        }

        int headerPages = (int)(header.HeaderSize + page_size - 1) / (int)page_size;
        int ramdiskPages = (ramdisk.Length + (int)page_size - 1) / (int)page_size;
        int dtbPages = (dtb.Length + (int)page_size - 1) / (int)page_size;

        int totalSize = (headerPages + ramdiskPages + dtbPages) * (int)page_size;
        byte[] buffer = new byte[totalSize];

        byte[] headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(ramdisk, 0, buffer, headerPages * page_size, ramdisk.Length);
        Array.Copy(dtb, 0, buffer, (headerPages + ramdiskPages) * page_size, dtb.Length);

        return buffer;
    }

    /// <summary>
    /// Generates Vendor Boot Image V4 data (containing Bootconfig)
    /// </summary>
    public byte[] CreateVendorBootImage4(byte[] ramdisk, byte[] dtb, string? cmdline, string? product_name, byte[]? bootconfig = null, uint page_size = 4096, uint base_addr = 0x10000000)
    {
        VendorBootImageHeaderV4 header = VendorBootImageHeaderV4.Create();
        header.PageSize = page_size;
        header.KernelAddr = base_addr + 0x00008000;
        header.RamdiskAddr = base_addr + 0x01000000;
        header.TagsAddr = base_addr + 0x00000100;
        header.VendorRamdiskSize = (uint)ramdisk.Length;
        header.DtbSize = (uint)dtb.Length;
        header.DtbAddr = (ulong)base_addr + 0x01100000;
        header.HeaderSize = (uint)Marshal.SizeOf<VendorBootImageHeaderV4>();
        header.BootconfigSize = (uint)(bootconfig?.Length ?? 0);

        if (!string.IsNullOrEmpty(cmdline))
        {
            byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdline);
            Array.Copy(cmdBytes, header.Cmdline, Math.Min(cmdBytes.Length, 2048));
        }

        if (!string.IsNullOrEmpty(product_name))
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(product_name);
            Array.Copy(nameBytes, header.Name, Math.Min(nameBytes.Length, 16));
        }

        int headerPages = (int)(header.HeaderSize + page_size - 1) / (int)page_size;
        int ramdiskPages = (ramdisk.Length + (int)page_size - 1) / (int)page_size;
        int dtbPages = (dtb.Length + (int)page_size - 1) / (int)page_size;
        int configPages = (int)((header.BootconfigSize + page_size - 1) / (int)page_size);

        int totalSize = (headerPages + ramdiskPages + dtbPages + configPages) * (int)page_size;
        byte[] buffer = new byte[totalSize];

        byte[] headerBytes = DataHelper.Struct2Bytes(header);
        Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
        Array.Copy(ramdisk, 0, buffer, headerPages * page_size, ramdisk.Length);
        Array.Copy(dtb, 0, buffer, (headerPages + ramdiskPages) * page_size, dtb.Length);
        if (bootconfig != null)
            Array.Copy(bootconfig, 0, buffer, (headerPages + ramdiskPages + dtbPages) * page_size, bootconfig.Length);

        return buffer;
    }

    /// <summary>
    /// Validates the requirements in android-info.txt
    /// </summary>
    public bool VerifyRequirements(string infoText, bool force = false)
    {
        var parser = new ProductInfoParser(this);
        if (!parser.Validate(infoText, out string? error))
        {
            if (force)
            {
                NotifyCurrentStep("WARNING: Requirements not met (ignored): " + error);
                return true;
            }
            throw new Exception(error);
        }
        return true;
    }

    /// <summary>
    /// Executes fastboot-info.txt commands
    /// </summary>
    public void FlashFromInfo(string infoContent, string imageDir, bool wipe = false, string? slotOverride = null, bool optimizeSuper = true)
    {
        NotifyCurrentStep("Parsing fastboot-info.txt...");
        var lines = infoContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string currentSlot = slotOverride ?? GetCurrentSlot();
        string otherSlot = currentSlot == "a" ? "b" : "a";
        LoadLogicalPartitionsFromMetadata(Path.Combine(imageDir, "super_empty.img"));
        var commands = new List<List<string>>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0) continue;

            if (parts[0] == "if-wipe")
            {
                if (!wipe) continue;
                parts.RemoveAt(0);
            }
            if (parts.Count > 0) commands.Add(parts);
        }
        if (IsUserspace())
        {
            foreach (var cmdParts in commands)
            {
                if (cmdParts[0] == "flash")
                {
                    string? part = GetPartitionFromArgs(cmdParts.GetRange(1, cmdParts.Count - 1));
                    if (part != null && IsLogicalOptimized(part))
                    {
                        try { ResizeLogicalPartition(part, 0); } catch { }
                    }
                }
            }
        }

        if (optimizeSuper && IsUserspace())
        {
            string emptyPath = Path.Combine(imageDir, "super_empty.img");
            if (File.Exists(emptyPath))
            {
                var logicalPartitionsToFlash = new List<(string Name, string Path)>();
                for (int i = 0; i < commands.Count; i++)
                {
                    var parts = commands[i];
                    if (parts[0] == "flash")
                    {
                        string? part = GetPartitionFromArgs(parts.GetRange(1, parts.Count - 1));
                        string? imgName = parts.Count > 2 ? parts[2] : part + ".img";
                        if (part != null && IsLogicalOptimized(part))
                        {
                            string imgPath = Path.Combine(imageDir, imgName!);
                            if (File.Exists(imgPath))
                            {
                                logicalPartitionsToFlash.Add((part, imgPath));
                                commands.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }

                if (logicalPartitionsToFlash.Count > 0)
                {
                    NotifyCurrentStep("Optimizing super partition flash from info...");
                    var helper = new SuperFlashHelper(this, "super", emptyPath);
                    foreach (var (name, path) in logicalPartitionsToFlash)
                    {
                        helper.AddPartition(name, path);
                    }
                    helper.Flash();
                }
            }
        }

        foreach (var parts in commands)
        {
            string cmd = parts[0];
            var args = parts.GetRange(1, parts.Count - 1);

            switch (cmd)
            {
                case "version":
                    if (args.Count > 0 && !CheckFastbootInfoRequirements(args[0]))
                        NotifyCurrentStep($"WARNING: Unsupported fastboot-info.txt version: {args[0]}");
                    break;
                case "flash":
                    ExecuteFlashTaskFromInfo(args, imageDir, currentSlot, otherSlot);
                    break;
                case "erase":
                    if (args.Count > 0) ErasePartition(args[0]);
                    break;
                case "reboot":
                    if (args.Count > 0) Reboot(args[0]);
                    else Reboot();
                    break;
                case "update-super":
                    string target = args.Count > 0 ? args[0] : "super";
                    string emptyPath = Path.Combine(imageDir, "super_empty.img");
                    if (File.Exists(emptyPath)) UpdateSuper(target, emptyPath);
                    break;
                default:
                    throw new InvalidDataException($"Unknown command in fastboot-info.txt: {cmd}");
            }
        }
    }

    private string? GetPartitionFromArgs(List<string> args)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--")) return arg;
        }
        return null;
    }

    private void ExecuteFlashTaskFromInfo(List<string> args, string imageDir, string currentSlot, string otherSlot)
    {
        bool applyVbmeta = false;
        string targetSlot = currentSlot;
        string? partition = null;
        string? imgName = null;

        foreach (var arg in args)
        {
            if (arg == "--apply-vbmeta") applyVbmeta = true;
            else if (arg == "--slot-other") targetSlot = otherSlot;
            else if (partition == null) partition = arg;
            else if (imgName == null) imgName = arg;
        }

        if (partition != null && imgName == null)
        {
            imgName = partition + ".img";
        }

        if (partition != null && imgName != null)
        {
            string imgPath = Path.Combine(imageDir, imgName);
            if (File.Exists(imgPath))
            {
                if (IsLogicalOptimized(partition))
                {
                    try { ResizeLogicalPartition(partition, 0); } catch { }
                }

                if (applyVbmeta || IsVbmetaPartition(partition))
                    FlashVbmeta(partition, imgPath);
                else
                    FlashImage(partition, imgPath, targetSlot);
            }
            else
            {
                NotifyCurrentStep($"WARNING: Image {imgName} for {partition} not found in {imageDir}");
            }
        }
    }

    public bool IsVbmetaPartition(string partition)
    {
        return partition.StartsWith("vbmeta", StringComparison.OrdinalIgnoreCase);
    }

    public bool CheckFastbootInfoRequirements(string version)
    {
        if (uint.TryParse(version, out uint v)) return v <= 2; // Support up to version 2
        return false;
    }

    /// <summary>
    /// Executes FlashAll (in a specified directory, finding and flashing base partitions)
    /// </summary>
    public void FlashAll(string productOutDir, bool wipe = false, bool skipSecondary = false, bool force = false, bool optimizeSuper = true)
    {
        CancelSnapshotIfNeeded();

        LoadLogicalPartitionsFromMetadata(Path.Combine(productOutDir, "super_empty.img"));

        string infoPath = Path.Combine(productOutDir, "fastboot-info.txt");
        if (File.Exists(infoPath))
        {
            NotifyCurrentStep("Using fastboot-info.txt for flashing...");
            FlashFromInfo(File.ReadAllText(infoPath), productOutDir, wipe, null, optimizeSuper);
            if (wipe) WipeUserData();
            return;
        }

        string productInfoPath = Path.Combine(productOutDir, "android-info.txt");
        if (File.Exists(productInfoPath))
        {
            VerifyRequirements(File.ReadAllText(productInfoPath), force);
        }

        var imageFiles = Directory.GetFiles(productOutDir, "*.img").ToList();
        List<string> physicalImages = new List<string>();
        List<string> logicalImages = new List<string>();

        foreach (var f in imageFiles)
        {
            string part = Path.GetFileNameWithoutExtension(f);
            if (IsLogicalOptimized(part)) logicalImages.Add(f);
            else physicalImages.Add(f);
        }

        physicalImages = physicalImages.OrderBy(f =>
        {
            string part = Path.GetFileNameWithoutExtension(f);
            if (part.EndsWith("_other", StringComparison.OrdinalIgnoreCase)) part = part.Substring(0, part.Length - 6);
            int index = Array.IndexOf(PartitionPriority, part.ToLower());
            return index == -1 ? int.MaxValue : index;
        }).ToList();

        string currentSlot = GetCurrentSlot();

        foreach (var filePath in physicalImages)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string part = fileName;
            string targetSlot = currentSlot;
            bool isOther = false;

            if (fileName.EndsWith("_other", StringComparison.OrdinalIgnoreCase))
            {
                part = fileName.Substring(0, fileName.Length - 6);
                targetSlot = currentSlot == "a" ? "b" : "a";
                isOther = true;
            }
            if (IsVbmetaPartition(part)) FlashVbmeta(part, filePath);
            else FlashImage(part, filePath, targetSlot);
            if (!isOther && !skipSecondary && HasSlot(part))
            {
                string otherSlot = currentSlot == "a" ? "b" : "a";
                if (IsVbmetaPartition(part)) FlashVbmeta(part, filePath);
                else FlashImage(part, filePath, otherSlot);
            }

            string sigPath = Path.Combine(productOutDir, fileName + ".sig");
            if (File.Exists(sigPath))
            {
                Signature(File.ReadAllBytes(sigPath));
            }
        }

        if (logicalImages.Count > 0)
        {
            if (optimizeSuper && IsUserspace())
            {
                NotifyCurrentStep("Optimizing super partition flash...");
                string emptyPath = Path.Combine(productOutDir, "super_empty.img");
                var helper = new SuperFlashHelper(this, "super", File.Exists(emptyPath) ? emptyPath : null);
                foreach (var logImg in logicalImages)
                {
                    helper.AddPartition(Path.GetFileNameWithoutExtension(logImg), logImg);
                }
                helper.Flash();
            }
            else
            {
                foreach (var logImg in logicalImages)
                {
                    string part = Path.GetFileNameWithoutExtension(logImg);
                    if (IsLogicalOptimized(part))
                    {
                        NotifyCurrentStep($"Preparing logical partition {part}...");
                        try
                        {
                            CreateLogicalPartition(part, 0);
                        }
                        catch { /* Ignore if already exists or not supported */ }

                        try { ResizeLogicalPartition(part, 0); } catch { }
                    }
                }

                foreach (var logImg in logicalImages)
                {
                    FlashImage(Path.GetFileNameWithoutExtension(logImg), logImg);
                }
            }
        }

        if (wipe)
        {
            WipeUserData();
        }
    }

    /// <summary>
    /// Clears user data, cache, and metadata partitions (corresponding to fastboot -w)
    /// </summary>
    public void WipeUserData()
    {
        string[] partitions = ["userdata", "cache", "metadata"];
        foreach (var partition in partitions)
        {
            try
            {
                string partitionType = GetPartitionType(partition);
                if (string.IsNullOrEmpty(partitionType)) continue;

                ErasePartition(partition);
                FormatPartition(partition);
            }
            catch { }
        }
    }

    private static void ReadStreamFully(Stream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}





