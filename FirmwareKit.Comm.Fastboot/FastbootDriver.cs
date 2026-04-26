using FirmwareKit.Comm.Fastboot.Network;
using FirmwareKit.Comm.Fastboot.Usb;
using FirmwareKit.Lp;
using System.IO.Compression;
using System.Text;

namespace FirmwareKit.Comm.Fastboot;

/// <summary>
/// Fastboot driver for communicating with Android devices via the fastboot protocol.
/// <para>用于通过 fastboot 协议与 Android 设备通信的驱动程序。</para>
/// </summary>
public partial class FastbootDriver : IDisposable
{
    /// <summary>
    /// Callback invoked when a flash/erase step finishes, with the step name, elapsed time, and success flag.
    /// <para>当刷写/擦除步骤完成时调用的回调，包含步骤名称、耗时和成功标志。</para>
    /// </summary>
    public Action<string, TimeSpan, bool>? OnStepFinished { get; set; }

    /// <summary>
    /// Gets or sets whether to convert sparse images to raw before flashing.
    /// <para>获取或设置是否在刷写前将稀疏镜像转换为原始镜像。</para>
    /// </summary>
    public bool ConvertSimgToRaw { get; set; }

    /// <summary>
    /// Releases the transport and all associated resources.
    /// <para>释放传输层及所有关联资源。</para>
    /// </summary>
    public void Dispose()
    {
        FastbootDebug.Log("Dispose()");
        Transport?.Dispose();
    }

    /// <summary>
    /// Resets the USB transport connection.
    /// <para>重置 USB 传输连接。</para>
    /// </summary>
    public void ResetTransport()
    {
        FastbootDebug.Log("ResetTransport()");
        if (Transport is UsbDevice usb)
        {
            usb.Reset();
        }
    }

    /// <summary>
    /// Checks whether the device is in userspace fastboot mode (fastbootd).
    /// <para>检查设备是否处于用户空间 fastboot 模式 (fastbootd)。</para>
    /// </summary>
    public bool IsUserspace()
    {
        FastbootDebug.Log("IsUserspace()");
        try
        {
            return GetVar("is-userspace", useCache: false, quiet: true) == "yes";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether a partition is logical, using preloaded metadata if available, otherwise querying the device.
    /// <para>检查分区是否为逻辑分区，优先使用预加载的元数据，否则查询设备。</para>
    /// </summary>
    public bool IsLogicalOptimized(string partition)
    {
        FastbootDebug.Log($"IsLogicalOptimized(partition={partition})");
        return _logicalPartitionsFromMetadata?.Contains(partition) ?? IsLogical(partition);
    }

    /// <summary>
    /// Loads logical partition names from a super image metadata file for optimized flashing.
    /// <para>从 super 镜像元数据文件加载逻辑分区名称，用于优化刷写。</para>
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
            var partitions = new HashSet<string>(
                meta.Partitions
                    .Select(p => p.Name.ToString())
                    .Where(n => !string.IsNullOrEmpty(n))!,
                StringComparer.Ordinal);
            _logicalPartitionsFromMetadata = partitions;
        }
        catch
        {
            _logicalPartitionsFromMetadata = null;
        }
    }

    /// <summary>
    /// Reads logical partition metadata from a super image file on disk.
    /// <para>从磁盘上的 super 镜像文件读取逻辑分区元数据。</para>
    /// </summary>
    public static LpMetadata ReadFromImageFile(string path)
    {
        FastbootDebug.Log($"ReadFromImageFile(path={path})");
        using var stream = File.OpenRead(path);
        return ReadFromImageStream(stream);
    }

    /// <summary>
    /// Reads logical partition metadata from a stream.
    /// <para>从流中读取逻辑分区元数据。</para>
    /// </summary>
    public static LpMetadata ReadFromImageStream(Stream stream)
    {
        FastbootDebug.Log($"ReadFromImageStream(stream={stream})");
        long[] tryOffsets = [
            MetadataFormat.LP_PARTITION_RESERVED_BYTES,
            MetadataFormat.LP_PARTITION_RESERVED_BYTES + MetadataFormat.LP_METADATA_GEOMETRY_SIZE,
            0
        ];

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
            }
        }
        throw new InvalidDataException("Valid LpMetadataGeometry not found. The image may not be a super image or may be corrupted.");
    }

    /// <summary>
    /// Gets the underlying transport used for fastboot communication.
    /// <para>获取用于 fastboot 通信的底层传输层。</para>
    /// </summary>
    public IFastbootTransport Transport { get; private set; }
    private Dictionary<string, string> _varCache = [];
    private Dictionary<string, bool> _hasSlotCache = [];
    private HashSet<string>? _logicalPartitionsFromMetadata;

    /// <summary>
    /// Initializes a new FastbootDriver with the specified transport.
    /// <para>使用指定的传输层初始化 FastbootDriver 的新实例。</para>
    /// </summary>
    public FastbootDriver(IFastbootTransport transport) => Transport = transport;

    /// <summary>
    /// Timeout in seconds for reading responses from the device.
    /// <para>从设备读取响应的超时时间（秒）。</para>
    /// </summary>
    public static int ReadTimeoutSeconds = 30;

    /// <summary>
    /// Maximum data size in bytes sent per single write operation.
    /// <para>每次单次写入操作发送的最大数据大小（字节）。</para>
    /// </summary>
    public static int OnceSendDataSize = 512 * 1024;

    /// <summary>
    /// Maximum download size for sparse image transfers.
    /// <para>稀疏镜像传输的最大下载大小。</para>
    /// </summary>
    public static long SparseMaxDownloadSize = uint.MaxValue;

    private static readonly string[] PartitionPriority =
    [
        "preloader", "bootloader", "radio", "dram", "md1img", "xbl", "abl", "keystore",
        "boot", "dtbo", "init_boot", "vendor_boot", "pvmfw",
        "vbmeta", "vbmeta_system", "vbmeta_vendor", "vbmeta_custom",
        "recovery", "system", "vendor", "product", "system_ext", "odm", "vendor_dlkm", "odm_dlkm", "system_dlkm"
    ];

    /// <summary>
    /// Raised when data is received from the device.
    /// <para>当从设备接收到数据时引发。</para>
    /// </summary>
    public event EventHandler<FastbootReceivedFromDeviceEventArgs>? ReceivedFromDevice;

    /// <summary>
    /// Raised when data transfer progress changes.
    /// <para>当数据传输进度变化时引发。</para>
    /// </summary>
    public event EventHandler<(long, long)>? DataTransferProgressChanged;

    /// <summary>
    /// Raised when the current flashing step changes.
    /// <para>当当前刷写步骤变化时引发。</para>
    /// </summary>
    public event EventHandler<string>? CurrentStepChanged;

    /// <summary>
    /// Raised when a fastboot command completes.
    /// <para>当 fastboot 命令完成时引发。</para>
    /// </summary>
    public event EventHandler<FastbootCommandEventArgs>? CommandCompleted;

    internal void NotifyCommandCompleted(string command, FastbootResponse response, bool quiet)
        => CommandCompleted?.Invoke(this, new FastbootCommandEventArgs(command, response, quiet));

    /// <summary>
    /// Notifies listeners that the current flashing step has changed.
    /// <para>通知监听器当前刷写步骤已变更。</para>
    /// </summary>
    public void NotifyCurrentStep(string step)
    {
        FastbootDebug.Log($"NotifyCurrentStep(step={step})");
        CurrentStepChanged?.Invoke(this, step);
    }

    /// <summary>
    /// Notifies listeners of data transfer progress.
    /// <para>通知监听器数据传输进度。</para>
    /// </summary>
    public void NotifyProgress(long current, long total)
    {
        FastbootDebug.Log($"NotifyProgress(current={current}, total={total})");
        DataTransferProgressChanged?.Invoke(this, (current, total));
    }

    /// <summary>
    /// Notifies listeners that data has been received from the device.
    /// <para>通知监听器已从设备接收到数据。</para>
    /// </summary>
    public void NotifyReceived(FastbootState state, string? info = null, string? text = null)
    {
        FastbootDebug.Log($"NotifyReceived(state={state}, info={info}, text={text})");
        ReceivedFromDevice?.Invoke(this, new FastbootReceivedFromDeviceEventArgs(state, info, text));
    }

    /// <summary>
    /// Checks whether the specified partition has A/B slots.
    /// <para>检查指定分区是否具有 A/B 槽位。</para>
    /// </summary>
    public bool HasSlot(string partition)
    {
        FastbootDebug.Log($"HasSlot(partition={partition})");
        if (string.IsNullOrEmpty(partition)) return false;
        if (_hasSlotCache.TryGetValue(partition, out bool has)) return has;

        try
        {
            has = GetVar("has-slot:" + partition) == "yes";
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
    /// Gets the current active slot suffix (e.g., "a" or "b").
    /// <para>获取当前活跃槽位后缀（如 "a" 或 "b"）。</para>
    /// </summary>
    public string GetCurrentSlot()
    {
        FastbootDebug.Log("GetCurrentSlot()");
        try
        {
            string slot = GetVar("current-slot");
            return slot.StartsWith("_") ? slot.Substring(1) : slot;
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Waits for a fastboot device to appear, with optional serial number filter and timeout.
    /// <para>等待 fastboot 设备出现，可选序列号过滤和超时。</para>
    /// </summary>
    public static FastbootDriver? WaitForDevice(Func<List<UsbDevice>> deviceFinder, string? serial = null, int timeoutSeconds = -1)
    {
        FastbootDebug.Log($"WaitForDevice(deviceFinder={deviceFinder}, serial={serial}, timeoutSeconds={timeoutSeconds})");
        DateTime start = DateTime.Now;
        while (timeoutSeconds == -1 || (DateTime.Now - start).TotalSeconds < timeoutSeconds)
        {
            var devices = deviceFinder();
            UsbDevice? found = string.IsNullOrEmpty(serial)
                ? devices.FirstOrDefault()
                : devices.FirstOrDefault(d =>
                {
                    try { d.GetSerialNumber(); return d.SerialNumber == serial; }
                    catch { return false; }
                });

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
    /// Queries all device variables via getvar:all and returns them as a dictionary.
    /// <para>通过 getvar:all 查询所有设备变量并以字典形式返回。</para>
    /// </summary>
    public Dictionary<string, string> GetVarAll()
    {
        FastbootDebug.Log("GetVarAll()");
        _varCache.Clear();
        try
        {
            var res = RawCommand("getvar:all").ThrowIfError();
            FastbootDebug.Log("Command response received. Parsing...");
            var dict = new Dictionary<string, string>();
            foreach (var line in res.Info)
            {
                FastbootDebug.Log("Parsing line: " + line);
                int colonIdx = line.LastIndexOf(':');
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
    /// Queries a single device variable by key. Results can be cached for performance.
    /// <para>按键查询单个设备变量。结果可缓存以提高性能。</para>
    /// </summary>
    public string GetVar(string key, bool useCache = true, bool quiet = false)
    {
        FastbootDebug.Log($"GetVar(key={key}, useCache={useCache}, quiet={quiet})");
        if (useCache && _varCache.TryGetValue(key, out string? cached)) return cached;
        var resObj = RawCommand("getvar:" + key, quiet);
        if (resObj.Result is FastbootState.Fail or FastbootState.Timeout)
        {
            return "";
        }
        var res = resObj.Response;
        if (useCache) _varCache[key] = res;
        return res;
    }

    /// <summary>
    /// Gets the number of A/B slots on the device.
    /// <para>获取设备上的 A/B 槽位数量。</para>
    /// </summary>
    public int GetSlotCount()
    {
        FastbootDebug.Log("GetSlotCount()");
        return int.TryParse(GetVar("slot-count"), out int count) ? count : 1;
    }

    /// <summary>
    /// Cancels a pending snapshot update if one is in progress.
    /// <para>如果正在进行快照更新，则取消挂起的快照更新。</para>
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
    /// Ensures the device is in userspace fastboot mode; reboots to fastbootd if necessary.
    /// <para>确保设备处于用户空间 fastboot 模式；必要时重启到 fastbootd。</para>
    /// </summary>
    public void EnsureUserspace()
    {
        if (IsUserspace()) return;

        NotifyCurrentStep("Operation requires fastbootd, rebooting...");
        Reboot("fastboot").ThrowIfError();

        _varCache.Clear();
        System.Threading.Thread.Sleep(1000);
        NotifyCurrentStep("waiting for any device >");

        Transport = Transport switch
        {
            UsbDevice usbDev => WaitForDevice(UsbManager.GetAllDevices, usbDev.SerialNumber, 30)?.Transport
                ?? throw new Exception("Failed to boot into userspace fastboot; one or more components might be unbootable."),
            TcpTransport tcp => ReconnectNetworkTransport(tcp.Host, tcp.Port, (h, p) => new TcpTransport(h, p))
                ?? throw new Exception("Failed to boot into userspace fastboot; one or more components might be unbootable."),
            UdpTransport udp => ReconnectNetworkTransport(udp.Host, udp.Port, (h, p) => new UdpTransport(h, p))
                ?? throw new Exception("Failed to boot into userspace fastboot; one or more components might be unbootable."),
            _ => throw new NotSupportedException("Automatic reboot to userspace is only supported for USB, TCP and UDP transports.")
        };

        DateTime userspaceWaitStart = DateTime.Now;
        while ((DateTime.Now - userspaceWaitStart).TotalSeconds < 30)
        {
            if (IsUserspace()) goto enteredUserspace;
            System.Threading.Thread.Sleep(1000);
        }
        throw new Exception("Failed to boot into userspace fastboot; one or more components might be unbootable.");

    enteredUserspace:
        _varCache.Clear();
    }

    private IFastbootTransport? ReconnectNetworkTransport(string host, int port, Func<string, int, IFastbootTransport> createTransport)
    {
        DateTime start = DateTime.Now;
        while ((DateTime.Now - start).TotalSeconds < 60)
        {
            try
            {
                return createTransport(host, port);
            }
            catch { System.Threading.Thread.Sleep(1000); }
        }
        return null;
    }

    /// <summary>
    /// Validates product info content against the connected device's properties.
    /// <para>根据已连接设备的属性验证产品信息内容。</para>
    /// </summary>
    public bool ValidateProductInfo(string content, out string? error)
        => new ProductInfoParser(this).Validate(content, out error);

    /// <summary>
    /// Retrieves staged data from the device and writes it to a file.
    /// <para>从设备获取暂存数据并写入文件。</para>
    /// </summary>
    public void GetStaged(string outputPath)
    {
        using var fs = File.Create(outputPath);
        GetStagedToStream(fs);
    }

    /// <summary>
    /// Retrieves staged data from the device and writes it to a stream.
    /// <para>从设备获取暂存数据并写入流。</para>
    /// </summary>
    public FastbootResponse GetStagedToStream(Stream output) => UploadData("get_staged", output);

    /// <summary>
    /// Dumps device info (bootloader version, baseband version, serial number) to the step notifier.
    /// <para>将设备信息（引导加载程序版本、基带版本、序列号）输出到步骤通知器。</para>
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
    /// Flashes all images from a ZIP archive to the device.
    /// <para>将 ZIP 归档中的所有镜像刷写到设备。</para>
    /// </summary>
    public void FlashZip(string zipPath, bool skipValidation = false, bool wipe = false, bool disableVerity = false, bool disableVerification = false)
    {
        CancelSnapshotIfNeeded();
        DumpInfo();

        string tempDir = Path.Combine(Path.GetTempPath(), "FirmwareKit.Comm.Fastboot_Zip_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            NotifyCurrentStep($"Extracting ZIP: {Path.GetFileName(zipPath)}");
            ZipFile.ExtractToDirectory(zipPath, tempDir);
            FlashAll(tempDir, wipe, false, skipValidation, true, disableVerity, disableVerification);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Gets the maximum download size supported by the device.
    /// <para>获取设备支持的最大下载大小。</para>
    /// </summary>
    public long GetMaxDownloadSize()
    {
        string? sizeStr = null;
        try { sizeStr = GetVar("max-download-size"); } catch { }
        if (string.IsNullOrEmpty(sizeStr)) return SparseMaxDownloadSize;

        long parsedSize = sizeStr!.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(sizeStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var hex) ? hex : -1
            : long.TryParse(sizeStr, out var dec) ? dec : -1;

        return parsedSize <= 0 ? SparseMaxDownloadSize : Math.Min(Math.Min(parsedSize, SparseMaxDownloadSize), uint.MaxValue);
    }

    /// <summary>
    /// Checks whether the device supports CRC verification for data transfers.
    /// <para>检查设备是否支持数据传输的 CRC 校验。</para>
    /// </summary>
    public bool HasCrc() => TryGetVar("has-crc", out var v) && v == "yes";

    /// <summary>
    /// Checks whether the specified partition exists on the device.
    /// <para>检查指定分区在设备上是否存在。</para>
    /// </summary>
    public bool PartitionExists(string partition)
        => TryGetVar("partition-size:" + partition, out var res) && !string.IsNullOrEmpty(res) && res != "0" && res != "0x0";

    /// <summary>
    /// Flashes an image file to the specified partition with optional slot override and super optimization control.
    /// <para>将镜像文件刷写到指定分区，可选槽位覆盖和 super 优化控制。</para>
    /// </summary>
    public void FlashImage(string partition, string filePath, string? slotOverride = null)
        => FlashImage(partition, filePath, slotOverride, false, null);

    /// <summary>
    /// Flashes an image file to the specified partition with full control over slot, super optimization, and progress callback.
    /// <para>将镜像文件刷写到指定分区，完全控制槽位、super 优化和进度回调。</para>
    /// </summary>
    public void FlashImage(string partition, string filePath, string? slotOverride, bool disableSuperOptimization, Action<string>? progressCallback)
    {
        FastbootDebug.Log($"FlashImage(partition={partition}, file={filePath}, slot={slotOverride ?? "null"}, disableSuperOptimization={disableSuperOptimization})");
        if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);

        if (partition.Equals("super", StringComparison.OrdinalIgnoreCase))
        {
            string? dir = Path.GetDirectoryName(filePath);
            string emptyPath = dir != null ? Path.Combine(dir, "super_empty.img") : "super_empty.img";
            if (!disableSuperOptimization && File.Exists(emptyPath) && IsUserspace())
            {
                progressCallback?.Invoke("[super] Optimizing super partition flash (AOSP style)...");
                var helper = new SuperFlashHelper(this, "super", emptyPath);
                var imgDir = dir ?? ".";
                var mergedParts = new List<string>();
                foreach (var img in Directory.GetFiles(imgDir, "*.img"))
                {
                    var partName = Path.GetFileNameWithoutExtension(img);
                    if (IsLogicalOptimized(partName))
                    {
                        helper.AddPartition(partName, img);
                        mergedParts.Add(partName);
                    }
                }
                if (mergedParts.Count > 0)
                {
                    progressCallback?.Invoke($"[super] Merged logical partitions: {string.Join(", ", mergedParts)}");
                }
                helper.Flash();
                progressCallback?.Invoke("[super] Flash complete. (Optimized super flash)");
                return;
            }
            else if (disableSuperOptimization)
            {
                progressCallback?.Invoke("[super] Super optimization disabled, will flash logical partitions separately if needed.");
            }
        }

        if (slotOverride == "all")
        {
            FlashImage(partition, filePath, "a");
            FlashImage(partition, filePath, "b");
            return;
        }

        string targetPartition = HasSlot(partition)
            ? partition + "_" + (slotOverride ?? GetCurrentSlot())
            : partition;

        FastbootDebug.Log($"Target Partition: {targetPartition}");

        if (IsLogicalOptimized(targetPartition))
        {
            try { ResizeLogicalPartition(targetPartition, 0); } catch { }
        }

        try
        {
            var fi = new FileInfo(filePath);
            using var fs = File.OpenRead(filePath);
            FlashUnsparseImage(targetPartition, fs, fi.Length).ThrowIfError();
        }
        catch (Exception ex)
        {
            FastbootDebug.Log("FlashImage Failed: " + ex);
            throw;
        }
    }

    /// <summary>
    /// Waits for a snapshot merge operation to complete within the specified timeout.
    /// <para>在指定超时时间内等待快照合并操作完成。</para>
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
            if (res is "none" or "completed") return;
            break;
        }
    }

    /// <summary>
    /// Flashes an image stream to the specified partition using the current active slot.
    /// <para>使用当前活跃槽位将镜像流刷写到指定分区。</para>
    /// </summary>
    public void FlashImage(string partition, Stream stream) => FlashImage(partition, stream, null);

    /// <summary>
    /// Flashes an image stream to the specified partition with optional slot override.
    /// <para>将镜像流刷写到指定分区，可选槽位覆盖。</para>
    /// </summary>
    public void FlashImage(string partition, Stream stream, string? slotOverride)
    {
        string targetPartition = HasSlot(partition)
            ? partition + "_" + (slotOverride ?? GetCurrentSlot())
            : partition;

        if (IsLogicalOptimized(targetPartition))
        {
            try { ResizeLogicalPartition(targetPartition, 0); } catch { }
        }

        try
        {
            FlashUnsparseImage(targetPartition, stream, stream.Length);
        }
        catch (Exception ex)
        {
            FastbootDebug.Log($"[ERROR] FlashImage failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Checks whether the specified partition is a logical partition on the device.
    /// <para>检查指定分区是否为设备上的逻辑分区。</para>
    /// </summary>
    public bool IsLogical(string partition)
        => TryGetVar("is-logical:" + partition, out var v) && v == "yes";

    /// <summary>
    /// Gets the size of the specified partition as a long integer (in bytes or hex).
    /// <para>以长整型获取指定分区的大小（字节或十六进制）。</para>
    /// </summary>
    public long GetPartitionSizeLong(string partition)
    {
        if (!TryGetVar("partition-size:" + partition, out var res) || string.IsNullOrEmpty(res)) return 0;
        return res.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt64(res, 16)
            : Convert.ToInt64(res);
    }

    /// <summary>
    /// Gets the raw size string of the specified partition from the device.
    /// <para>从设备获取指定分区的原始大小字符串。</para>
    /// </summary>
    public string GetPartitionSize(string partition)
        => TryGetVar("partition-size:" + partition, out var v) ? v : "";

    /// <summary>
    /// Gets the filesystem type of the specified partition.
    /// <para>获取指定分区的文件系统类型。</para>
    /// </summary>
    public string GetPartitionType(string partition)
        => TryGetVar("partition-type:" + partition, out var v) ? v : "";

    /// <summary>
    /// Formats a partition locally by creating an empty filesystem image and flashing it.
    /// <para>通过创建空文件系统镜像并刷写来本地格式化分区。</para>
    /// </summary>
    [ExternalToolDependency("mke2fs")]
    [ExternalToolDependency("make_f2fs")]
    public void FormatPartitionLocal(string partition, string fsType = "ext4", long size = 0)
    {
        if (size <= 0)
        {
            if (TryGetVar("partition-size:" + partition, out var res) && !string.IsNullOrEmpty(res))
            {
                size = res.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToInt64(res, 16)
                    : Convert.ToInt64(res);
            }
        }
        if (size <= 0) size = 1024 * 1024 * 32;

        string tmpFile = Path.GetTempFileName();
        try
        {
            switch (fsType)
            {
                case "ext4": FileSystemUtil.CreateEmptyExt4(tmpFile, size); break;
                case "f2fs": FileSystemUtil.CreateEmptyF2fs(tmpFile, size); break;
                default: throw new NotSupportedException("fs type not supported: " + fsType);
            }
            FlashImage(partition, tmpFile);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    /// <summary>
    /// Verifies that the device meets the requirements specified in the product info text.
    /// <para>验证设备是否满足产品信息文本中指定的要求。</para>
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
    /// Flashes images according to a fastboot-info.txt content string.
    /// <para>根据 fastboot-info.txt 内容字符串刷写镜像。</para>
    /// </summary>
    public void FlashFromInfo(string infoContent, string imageDir, bool wipe = false, string? slotOverride = null, bool optimizeSuper = true, bool disableVerity = false, bool disableVerification = false)
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

            var parts = trimmed.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
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
                    ExecuteFlashTaskFromInfo(args, imageDir, currentSlot, otherSlot, disableVerity, disableVerification);
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

    private void ExecuteFlashTaskFromInfo(List<string> args, string imageDir, string currentSlot, string otherSlot, bool disableVerity, bool disableVerification)
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

        imgName ??= partition + ".img";

        if (partition != null)
        {
            string imgPath = Path.Combine(imageDir, imgName);
            if (File.Exists(imgPath))
            {
                if (IsLogicalOptimized(partition))
                {
                    try { ResizeLogicalPartition(partition, 0); } catch { }
                }

                if (applyVbmeta || IsVbmetaPartition(partition))
                    FlashVbmeta(partition, imgPath, disableVerity, disableVerification);
                else
                    FlashImage(partition, imgPath, targetSlot);
            }
            else
            {
                NotifyCurrentStep($"WARNING: Image {imgName} for {partition} not found in {imageDir}");
            }
        }
    }

    /// <summary>
    /// Checks whether the specified partition name starts with "vbmeta".
    /// <para>检查指定分区名称是否以 "vbmeta" 开头。</para>
    /// </summary>
    public bool IsVbmetaPartition(string partition)
        => partition.StartsWith("vbmeta", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the fastboot-info.txt version is supported (version ≤ 2).
    /// <para>检查 fastboot-info.txt 版本是否受支持（版本 ≤ 2）。</para>
    /// </summary>
    public bool CheckFastbootInfoRequirements(string version)
        => uint.TryParse(version, out uint v) && v <= 2;

    /// <summary>
    /// Flashes all images from a product output directory to the device, handling physical, logical, and A/B partitions.
    /// <para>将产品输出目录中的所有镜像刷写到设备，处理物理分区、逻辑分区和 A/B 分区。</para>
    /// </summary>
    public void FlashAll(string productOutDir, bool wipe = false, bool skipSecondary = false, bool force = false, bool optimizeSuper = true, bool disableVerity = false, bool disableVerification = false, bool disableFastbootInfo = false, bool excludeDynamicPartitions = false)
    {
        CancelSnapshotIfNeeded();

        LoadLogicalPartitionsFromMetadata(Path.Combine(productOutDir, "super_empty.img"));

        string infoPath = Path.Combine(productOutDir, "fastboot-info.txt");
        if (!disableFastbootInfo && File.Exists(infoPath))
        {
            NotifyCurrentStep("Using fastboot-info.txt for flashing...");
            FlashFromInfo(File.ReadAllText(infoPath), productOutDir, wipe, null, optimizeSuper, disableVerity, disableVerification);
            if (wipe) WipeUserData();
            return;
        }

        string productInfoPath = Path.Combine(productOutDir, "android-info.txt");
        if (File.Exists(productInfoPath))
        {
            VerifyRequirements(File.ReadAllText(productInfoPath), force);
        }

        var imageFiles = Directory.GetFiles(productOutDir, "*.img");
        var physicalImages = new List<string>();
        var logicalImages = new List<string>();

        foreach (var f in imageFiles)
        {
            string part = Path.GetFileNameWithoutExtension(f);
            if (IsLogicalOptimized(part))
            {
                if (!excludeDynamicPartitions) logicalImages.Add(f);
            }
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
            if (IsVbmetaPartition(part)) FlashVbmeta(part, filePath, disableVerity, disableVerification);
            else FlashImage(part, filePath, targetSlot);
            if (!isOther && !skipSecondary && HasSlot(part))
            {
                string otherSlot = currentSlot == "a" ? "b" : "a";
                if (IsVbmetaPartition(part)) FlashVbmeta(part, filePath, disableVerity, disableVerification);
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
                        try { CreateLogicalPartition(part, 0); } catch { }
                        try { ResizeLogicalPartition(part, 0); } catch { }
                    }
                }

                foreach (var logImg in logicalImages)
                {
                    FlashImage(Path.GetFileNameWithoutExtension(logImg), logImg);
                }
            }
        }

        if (wipe) WipeUserData();
    }

    /// <summary>
    /// Wipes user data by erasing and formatting userdata, cache, and metadata partitions.
    /// <para>通过擦除并格式化 userdata、cache 和 metadata 分区来清除用户数据。</para>
    /// </summary>
    public void WipeUserData()
    {
        foreach (var partition in new[] { "userdata", "cache", "metadata" })
        {
            try
            {
                string partitionType = GetPartitionType(partition);
                if (string.IsNullOrEmpty(partitionType)) continue;

                ErasePartitionNoSlot(partition);
                FormatPartition(partition);
            }
            catch { }
        }
    }

    private bool TryGetVar(string key, out string value)
    {
        try
        {
            value = GetVar(key);
            return true;
        }
        catch
        {
            value = "";
            return false;
        }
    }
}
