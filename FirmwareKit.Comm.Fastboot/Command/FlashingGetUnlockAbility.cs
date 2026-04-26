namespace FirmwareKit.Comm.Fastboot;

public partial class FastbootDriver
{
    /// <summary>
    /// Gets the flashing unlock ability value. Returns 0 if unavailable or on failure.
    /// <para>获取 flashing 解锁能力值。如果不可用或失败则返回 0。</para>
    /// </summary>
    public int FlashingGetUnlockAbility()
    {
        var res = FlashingCommand("get_unlock_ability");
        return res.Result == FastbootState.Success && int.TryParse(res.Response?.Trim(), out int ability) ? ability : 0;
    }
}
