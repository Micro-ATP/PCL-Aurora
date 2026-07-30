namespace PCL.Aurora.Application;

/// <summary>
/// 下载执行器的安全设置边界与速度档位。
///
/// 速度档位直接适配自 PCL-CE 的
/// Plain Craft Launcher 2/Modules/Base/ModSetup.cs（ToolDownloadSpeed），
/// 并将 Windows 全局下载器替换为跨平台的单次安装执行器设置。
/// </summary>
public static class LauncherDownloadSettings
{
    public const int MinimumConcurrency = 1;
    public const int DefaultConcurrency = 64;
    public const int MaximumConcurrency = 256;

    public const int MinimumSpeedLimitStep = 0;
    public const int UnlimitedSpeedLimitStep = 42;

    public static bool IsValidConcurrency(int value) =>
        value is >= MinimumConcurrency and <= MaximumConcurrency;

    public static bool IsValidSpeedLimitStep(int value) =>
        value is >= MinimumSpeedLimitStep and <= UnlimitedSpeedLimitStep;

    /// <summary>
    /// 返回每秒最大字节数；<see langword="null"/> 表示不限速。
    /// </summary>
    public static long? GetSpeedLimitBytesPerSecond(int step)
    {
        if (!IsValidSpeedLimitStep(step))
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }

        // PCL-CE 的 0–42 档位映射：0.1–1.5、2–10、11–20 MiB/s，42 为不限速。
        return step switch
        {
            <= 14 => (long)Math.Round((step + 1) * 0.1d * 1024d * 1024d),
            <= 31 => (long)Math.Round((step - 11) * 0.5d * 1024d * 1024d),
            <= 41 => (step - 21) * 1024 * 1024L,
            _ => null,
        };
    }

    public static string GetSpeedLimitDisplayName(int step)
    {
        var bytesPerSecond = GetSpeedLimitBytesPerSecond(step);
        if (bytesPerSecond is null)
        {
            return "不限速";
        }

        var mebibytes = bytesPerSecond.Value / 1024d / 1024d;
        return $"{mebibytes:0.#} MiB/s";
    }
}
