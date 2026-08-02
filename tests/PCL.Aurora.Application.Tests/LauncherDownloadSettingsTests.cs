using PCL.Aurora.Application;

namespace PCL.Aurora.Application.Tests;

public sealed class LauncherDownloadSettingsTests
{
    [Theory]
    [InlineData(0, 104858L)]
    [InlineData(14, 1572864L)]
    [InlineData(15, 2097152L)]
    [InlineData(31, 10485760L)]
    [InlineData(32, 11534336L)]
    [InlineData(41, 20971520L)]
    public void GetSpeedLimitBytesPerSecond_UsesPclCeCompatibleSteps(int step, long expected)
    {
        Assert.Equal(expected, LauncherDownloadSettings.GetSpeedLimitBytesPerSecond(step));
    }

    [Fact]
    public void GetSpeedLimitBytesPerSecond_UsesStep42ForUnlimited()
    {
        Assert.Null(LauncherDownloadSettings.GetSpeedLimitBytesPerSecond(42));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(LauncherDownloadSettings.MaximumConcurrency + 1)]
    public void Preferences_RejectUnsafeDownloadSettings(int concurrency)
    {
        var preferences = new LauncherPreferences(LauncherThemeMode.System, DownloadConcurrency: concurrency);

        Assert.False(preferences.IsValid);
    }
}
