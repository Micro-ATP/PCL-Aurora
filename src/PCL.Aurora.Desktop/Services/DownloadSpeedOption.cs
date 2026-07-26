using PCL.Aurora.Application;

namespace PCL.Aurora.Desktop.Services;

public sealed record DownloadSpeedOption(int Step, string DisplayName)
{
    public static IReadOnlyList<DownloadSpeedOption> CreateAll() =>
        Enumerable.Range(
                LauncherDownloadSettings.MinimumSpeedLimitStep,
                LauncherDownloadSettings.UnlimitedSpeedLimitStep - LauncherDownloadSettings.MinimumSpeedLimitStep + 1)
            .Select(step => new DownloadSpeedOption(step, LauncherDownloadSettings.GetSpeedLimitDisplayName(step)))
            .ToArray();
}
