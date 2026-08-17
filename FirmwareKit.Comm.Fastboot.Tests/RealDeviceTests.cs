using FirmwareKit.Comm.Fastboot.Usb;
using System.Text;

namespace FirmwareKit.Comm.Fastboot.Tests;

// ============================================================================
// 真机非破坏性测试集（Real-Device Non-Destructive Test Suite）
//
// 用途：
//   基于 fastboot_commands_dataset.txt 与 AOSP fastboot 审计结果，对连接到本机的
//   真实 fastboot 设备做功能/行为验证。只执行只读/无破坏性命令，
//   绝不执行 flash / erase / format / update / wipe / unlock / lock 等破坏性操作。
//
// 运行前提（环境变量）：
//   FASTBOOT_REAL_DEVICE=1            主开关，未设置时全部用例自动 Skip
//   FASTBOOT_SERIAL=<serial>          可选：只对指定序列号设备执行
//   FASTBOOT_ALLOW_REBOOT=1           可选：允许执行 reboot-bootloader / reboot-fastboot
//   FASTBOOT_ALLOW_DOWNLOAD=1         可选：允许向设备 RAM 下载小数据（不 flash）
//   FASTBOOT_ALLOW_FETCH=1            可选：允许 fetch 读取分区（需 fastbootd）
//
// 安全护栏：
//   1. 本类使用 RunSafe() 统一发出裸命令，任何匹配 DestructivePrefixes 的命令
//      会直接抛异常，从源头杜绝误刷写。
//   2. 用例清单只覆盖数据集中的非破坏性子集（见各用例注释中的数据集编号）。
//   3. 失败降级：getvar 返回空串/设备 FAIL 均视为可接受，不抛异常；
//      仅当库本身行为违背协议契约时才判失败。
// ============================================================================

public sealed class RealDeviceFixture : IDisposable
{
    private const string EnableEnv = "FASTBOOT_REAL_DEVICE";
    private const string SerialEnv = "FASTBOOT_SERIAL";

    public FastbootDriver? Driver { get; private set; }
    public string Serial { get; private set; } = "";
    public string? SkipReason { get; private set; }

    public RealDeviceFixture()
    {
        if (Environment.GetEnvironmentVariable(EnableEnv) != "1")
        {
            SkipReason = $"Real-device tests disabled: set {EnableEnv}=1 to run.";
            return;
        }

        try
        {
            var wanted = Environment.GetEnvironmentVariable(SerialEnv);
            var devices = UsbManager.GetAllDevices();
            UsbDevice? found = null;

            if (!string.IsNullOrEmpty(wanted))
            {
                found = devices.FirstOrDefault(d =>
                {
                    try { d.GetSerialNumber(); return d.SerialNumber == wanted; }
                    catch { return false; }
                });
            }
            else
            {
                found = devices.FirstOrDefault();
            }

            if (found == null)
            {
                foreach (var d in devices) d.Dispose();
                SkipReason = string.IsNullOrEmpty(wanted)
                    ? "No fastboot device found over USB."
                    : $"No fastboot device with serial '{wanted}' found over USB.";
                return;
            }

            foreach (var d in devices) if (d != found) d.Dispose();
            Driver = new FastbootDriver(found);
            Serial = found.SerialNumber ?? "";
        }
        catch (Exception ex)
        {
            SkipReason = "Device acquisition failed: " + ex.Message;
        }
    }

    /// <summary>Returns the driver or skips the current test when no device is available.</summary>
    public FastbootDriver Require()
    {
        if (SkipReason != null) Assert.Skip(SkipReason);
        return Driver!;
    }

    /// <summary>Replaces the cached driver (used after a reboot re-acquires the device).</summary>
    public void ReplaceDriver(FastbootDriver newDriver)
    {
        Driver?.Dispose();
        Driver = newDriver;
    }

    public void Dispose() => Driver?.Dispose();
}

[Trait("Category", "RealDevice")]
public sealed class RealDeviceTests : IClassFixture<RealDeviceFixture>
{
    private const string AllowRebootEnv = "FASTBOOT_ALLOW_REBOOT";
    private const string AllowDownloadEnv = "FASTBOOT_ALLOW_DOWNLOAD";
    private const string AllowFetchEnv = "FASTBOOT_ALLOW_FETCH";

    // 破坏性 / 状态改变命令黑名单：测试套件任何路径都不得发出这些命令。
    // （fastboot_commands_dataset.txt 中 D/E/F/G/H/I 类命令大多落入此表，被排除在真机测试外）
    private static readonly string[] DestructivePrefixes =
    {
        "flash", "erase", "format", "update", "wipe-super", "update-super",
        "create-logical-partition", "delete-logical-partition", "resize-logical-partition",
        "oem unlock", "oem lock", "flashing unlock", "flashing lock",
        "snapshot-update", "set_active", "stash", "boot", "continue"
    };

    private readonly RealDeviceFixture _fixture;

    public RealDeviceTests(RealDeviceFixture fixture) => _fixture = fixture;

    /// <summary>Unified raw-command entry with a destructive-command guard.</summary>
    private static FastbootResponse RunSafe(FastbootDriver driver, string command)
    {
        foreach (var prefix in DestructivePrefixes)
        {
            if (command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Destructive command blocked by test-suite safety guard: '{command}'");
            }
        }
        return driver.RawCommand(command);
    }

    private static bool IsEnabled(string env) => Environment.GetEnvironmentVariable(env) == "1";

    // ============================ A. 设备发现 ============================

    [Fact]
    public void Devices_EnumeratesAtLeastOneDevice()
    {
        // 数据集 A: fastboot devices
        var devices = UsbManager.GetAllDevices();
        try
        {
            if (devices.Count == 0) Assert.Skip("No fastboot device found over USB.");
            Assert.True(devices.Count >= 1);
        }
        finally
        {
            foreach (var d in devices) d.Dispose();
        }
    }

    [Fact]
    public void Device_Serial_IsReadable()
    {
        var driver = _fixture.Require();
        Assert.False(string.IsNullOrEmpty(_fixture.Serial), "device serial should be readable");
        // 与数据集 B: getvar serialno 对照
        string serialno = driver.GetVar("serialno");
        Assert.True(string.IsNullOrEmpty(serialno) || serialno == _fixture.Serial,
            $"getvar serialno '{serialno}' should match enumerated serial '{_fixture.Serial}'");
    }

    // ============================ B. 设备信息采集 ============================

    [Theory]
    [InlineData("is-userspace")]
    [InlineData("version-vndk")]
    [InlineData("variant")]
    [InlineData("unlocked")]
    [InlineData("product")]
    [InlineData("current-slot")]
    [InlineData("snapshot-update-status")]
    public void GetVar_DataSetVars_NoThrow_And_Graceful(string key)
    {
        // 数据集 B/I 的全部 getvar 键；设备不支持时库以 Fail + ErrorMessage 优雅返回（不得抛异常）
        var driver = _fixture.Require();
        var response = driver.GetVarWithResult(key);
        if (response.Success)
        {
            Assert.NotNull(response.Value);
        }
        else
        {
            Assert.False(string.IsNullOrEmpty(response.ErrorMessage),
                $"getvar:{key} failed but carried no error message");
        }
    }

    [Fact]
    public void GetVar_Version_NoThrow_Graceful()
    {
        // version 属 AOSP 常见变量但并非所有 bootloader 都上报（实测三星 SM6150 机型不回 version）。
        // 库契约：设备不上报时返回 ""，不得抛异常。
        var driver = _fixture.Require();
        string version = driver.GetVar("version");
        Assert.NotNull(version);
        Assert.True(string.IsNullOrEmpty(version) || version.Length > 0,
            "getvar version 要么为空（设备未上报）要么有值");
    }

    [Fact]
    public void GetVar_UnknownVariable_ReturnsEmpty_WithoutThrowing()
    {
        // 设备对未知变量应回 FAIL；库约定返回 "" 而非抛异常（与 AOSP GetVar 语义兼容）
        var driver = _fixture.Require();
        string value = driver.GetVar("no-such-variable-xyz");
        Assert.Equal("", value);
    }

    [Fact]
    public void GetVarAll_Parses_ExpectedKeys()
    {
        // 数据集 A/B: getvar all。version 变量部分 bootloader 不上报（实测三星机型缺失），
        // 因此只要求必备键（product/serialno 至少其一）与解析完整性。
        var driver = _fixture.Require();
        var vars = driver.GetVarAll();
        Assert.NotEmpty(vars);
        Assert.True(vars.ContainsKey("product") || vars.ContainsKey("serialno"),
            "getvar all should include product/serialno");
    }

    [Fact]
    public void GetVarAll_Parses_ColonInKey_Values()
    {
        // 审计结论：partition-size:<name>:0x... 等含冒号的键用 LastIndexOf(':') 拆分，
        // 键保留 "partition-size:<name>"，值保留 "0x..."（与 AOSP Partitions() 正则一致）
        var driver = _fixture.Require();
        var vars = driver.GetVarAll();
        foreach (var kv in vars)
        {
            if (kv.Key.StartsWith("partition-size:", StringComparison.OrdinalIgnoreCase))
            {
                Assert.StartsWith("0x", kv.Value, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void ProbeCapabilities_IsProbed_And_Consistent()
    {
        // 库扩展能力探测（非 AOSP CLI 命令，纯 getvar 组合）。
        // ProtocolVersion 依赖 getvar version，部分 bootloader 不上报（实测三星机型缺失），
        // 因此允许为空；序列号必须可读。
        var driver = _fixture.Require();
        var caps = driver.ProbeCapabilities();
        Assert.True(caps.IsProbed);
        Assert.False(string.IsNullOrEmpty(caps.SerialNumber) && string.IsNullOrEmpty(caps.ProtocolVersion),
            "serialno/version 至少其一应可读");
    }

    // ============================ 分区只读查询 ============================

    [Fact]
    public void Partition_HasSlot_Boot_NoThrow()
    {
        var driver = _fixture.Require();
        bool has = driver.HasSlot("boot"); // 内部 getvar has-slot:boot，只读
        _ = has;
        Assert.True(true); // 不允许此查询失败；有/无槽位都合法
    }

    [Fact]
    public void Partition_IsLogical_NoThrow()
    {
        var driver = _fixture.Require();
        bool isLogical = driver.IsLogical("system"); // 内部 getvar is-logical:system，只读
        _ = isLogical;
        Assert.True(true);
    }

    [Fact]
    public void Partition_Size_And_Type_Queryable()
    {
        // 数据集 A 中 Modifypartition 依赖 partition-size；此处只读校验格式
        var driver = _fixture.Require();
        string size = driver.GetPartitionSize("boot");
        string type = driver.GetPartitionType("boot");

        if (!string.IsNullOrEmpty(size))
        {
            Assert.Matches("^(0x[0-9a-fA-F]+|\\d+)$", size);
            Assert.True(driver.GetPartitionSizeLong("boot") > 0);
        }
        if (!string.IsNullOrEmpty(type))
        {
            Assert.False(type.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase));
        }
        // 设备未上报 partition-size 时为空串属合法降级
        Assert.True(driver.PartitionExists("boot") || string.IsNullOrEmpty(size));
    }

    [Fact]
    public void GetMaxDownloadSize_Returns_Positive()
    {
        // AOSP FB_VAR_MAX_DOWNLOAD_SIZE；无上报时走 256MiB 保守回退，必须 > 0
        var driver = _fixture.Require();
        Assert.True(driver.GetMaxDownloadSize() > 0);
    }

    // ============================ 输出风格（审计对照） ============================

    [Fact]
    public void DumpInfo_Raises_Steps_MatchingAospFormat()
    {
        // 审计结论：本地 DumpInfo 文案与 AOSP fastboot.cpp DumpInfo() 逐字一致
        var driver = _fixture.Require();
        var steps = new List<string>();
        driver.CurrentStepChanged += (_, step) => steps.Add(step);
        driver.DumpInfo();

        Assert.Equal("--------------------------------------------", steps[0]);
        Assert.StartsWith("Bootloader Version...: ", steps[1]);
        Assert.StartsWith("Baseband Version.....: ", steps[2]);
        Assert.StartsWith("Serial Number........: ", steps[3]);
        Assert.Equal("--------------------------------------------", steps[4]);
    }

    [Fact]
    public void ReceivedFromDevice_Fires_ForGetVarAll()
    {
        // 审计结论：INFO/TEXT 帧通过事件暴露（对应 AOSP 的 info_/text_ 回调）
        var driver = _fixture.Require();
        int infoEvents = 0;
        driver.ReceivedFromDevice += (_, e) =>
        {
            if (e.Type == FastbootState.Info) infoEvents++;
        };
        var vars = driver.GetVarAll();
        Assert.NotEmpty(vars);
        Assert.True(infoEvents > 0, "getvar:all 应产生 INFO 事件（AOSP info 回调语义）");
    }

    // ============================ 可选：RAM 下载（不 flash） ============================

    [Fact]
    public void Download_SmallBuffer_ToRam_NoFlash_OptIn()
    {
        // 数据集 E 中 flash 的前置步骤是 download；此处只验证 download 到设备 RAM，
        // 绝不跟 flash。设备缓冲数据于 RAM，不落盘，属非破坏。默认关闭。
        if (!IsEnabled(AllowDownloadEnv)) Assert.Skip($"Set {AllowDownloadEnv}=1 to run.");

        var driver = _fixture.Require();
        byte[] payload = Encoding.ASCII.GetBytes("FirmwareKit real-device download test payload.");
        var response = driver.DownloadData(payload);
        Assert.Equal(FastbootState.Success, response.Result);
    }

    // ============================ 可选：fetch 只读拉取分区 ============================

    [Fact]
    public void Fetch_BootPartition_ToTempFile_ReadOnly_OptIn()
    {
        // 数据集 J 未含 fetch，但审计确认 fetch 为只读拉取（写本地文件，不改设备）。
        // 需要 fastbootd（is-userspace=yes）与 max-fetch-size 支持。默认关闭。
        if (!IsEnabled(AllowFetchEnv)) Assert.Skip($"Set {AllowFetchEnv}=1 to run.");

        var driver = _fixture.Require();
        if (driver.GetVar("is-userspace") != "yes")
            Assert.Skip("fetch 需要 fastbootd（getvar is-userspace != yes），已跳过。");

        string tmp = Path.Combine(Path.GetTempPath(), "firmwarekit_fetch_" + Guid.NewGuid().ToString("N") + ".img");
        try
        {
            var response = driver.Fetch("boot", tmp);
            if (response.Result == FastbootState.Fail &&
                response.Response.Contains("unknown", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Skip("设备不支持 fetch 命令，已跳过。");
            }
            Assert.Equal(FastbootState.Success, response.Result);
            Assert.True(new FileInfo(tmp).Length > 0, "fetch 结果文件不应为空");
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    // ============================ 可选：重启回 fastboot（无数据破坏） ============================

    [Fact]
    public void Reboot_Bootloader_ReturnsToFastboot_OptIn()
    {
        // 数据集 C: reboot-bootloader。重启后仍处于 bootloader(fastboot) 模式，
        // 用 WaitForDevice 重新接管；不刷任何分区，无数据破坏。默认关闭。
        if (!IsEnabled(AllowRebootEnv)) Assert.Skip($"Set {AllowRebootEnv}=1 to run.");

        var driver = _fixture.Require();
        var res = driver.Reboot("bootloader");
        Assert.True(res.Result is FastbootState.Success or FastbootState.Fail,
            "reboot-bootloader 应答应可解析（成功或设备FAIL均可）");

        var reacquired = FastbootDriver.WaitForDevice(
            UsbManager.GetAllDevices, _fixture.Serial, timeoutSeconds: 60);
        if (reacquired == null) Assert.Skip("reboot 后设备未在 60s 内重现，已跳过后续校验。");
        _fixture.ReplaceDriver(reacquired);

        Assert.False(string.IsNullOrEmpty(_fixture.Require().GetVar("version")),
            "重启后应仍可 getvar version");
    }

    [Fact]
    public void Reboot_Fastboot_ReturnsToFastbootd_OptIn()
    {
        // 数据集 C: reboot-fastboot（进入 fastbootd）。需设备支持，不支持则设备回 FAIL。
        if (!IsEnabled(AllowRebootEnv)) Assert.Skip($"Set {AllowRebootEnv}=1 to run.");

        var driver = _fixture.Require();
        var res = driver.Reboot("fastboot");
        Assert.True(res.Result is FastbootState.Success or FastbootState.Fail,
            "reboot-fastboot 应答应可解析（成功或设备FAIL均可）");

        var reacquired = FastbootDriver.WaitForDevice(
            UsbManager.GetAllDevices, _fixture.Serial, timeoutSeconds: 60);
        if (reacquired == null) Assert.Skip("reboot 后设备未在 60s 内重现，已跳过后续校验。");
        _fixture.ReplaceDriver(reacquired);

        var userspace = _fixture.Require().GetVar("is-userspace");
        Assert.True(userspace == "yes" || userspace == "no" || userspace == "",
            "is-userspace 返回值应为 yes/no/空串");
    }

    // ============================ 安全护栏验证 ============================

    [Theory]
    [InlineData("flash:boot")]
    [InlineData("erase:userdata")]
    [InlineData("format:userdata")]
    [InlineData("update:ota.zip")]
    [InlineData("wipe-super:super")]
    [InlineData("create-logical-partition:test:00")]
    [InlineData("delete-logical-partition:test")]
    [InlineData("flashing unlock")]
    [InlineData("oem unlock")]
    [InlineData("snapshot-update:cancel")]
    [InlineData("set_active:b")]
    public void SafetyGuard_Blocks_DestructiveCommands(string command)
    {
        // 护栏自检：破坏性命令即使被误调用也必须被 RunSafe 拒绝
        var ex = Assert.Throws<InvalidOperationException>(() => RunSafe(_fixture.Require(), command));
        Assert.Contains("safety guard", ex.Message);
    }

    [Fact]
    public void SafetyGuard_Allows_ReadOnlyCommands()
    {
        // 护栏自检：只读命令必须放行（getvar 家族）
        var driver = _fixture.Require();
        var response = RunSafe(driver, "getvar:version");
        Assert.True(response.Result is FastbootState.Success or FastbootState.Fail);
    }
}
