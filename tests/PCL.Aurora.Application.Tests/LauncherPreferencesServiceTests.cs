using PCL.Aurora.Application;

namespace PCL.Aurora.Application.Tests;

public sealed class LauncherPreferencesServiceTests
{
    [Fact]
    public async Task SaveThemeModeAsync_PreservesLoadedInstanceSelection()
    {
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(new LauncherPreferences(LauncherThemeMode.System, "1.21.4", "Aurora_01"), null));
        var service = new LauncherPreferencesService(store);
        await service.LoadAsync();

        await service.SaveThemeModeAsync(LauncherThemeMode.Dark);

        Assert.Equal(LauncherThemeMode.Dark, store.SavedPreferences?.ThemeMode);
        Assert.Equal("1.21.4", store.SavedPreferences?.SelectedInstanceName);
        Assert.Equal("Aurora_01", store.SavedPreferences?.OfflinePlayerName);
    }

    [Fact]
    public async Task SaveDownloadSettingsAsync_PreservesPreviouslyLoadedPreferences()
    {
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(new LauncherPreferences(LauncherThemeMode.Light, "1.21.4", "Aurora_01"), null));
        var service = new LauncherPreferencesService(store);
        await service.LoadAsync();

        await service.SaveDownloadConcurrencyAsync(8);
        await service.SaveDownloadSpeedLimitStepAsync(31);

        Assert.Equal(8, store.SavedPreferences?.DownloadConcurrency);
        Assert.Equal(31, store.SavedPreferences?.DownloadSpeedLimitStep);
        Assert.Equal("1.21.4", store.SavedPreferences?.SelectedInstanceName);
        Assert.Equal("Aurora_01", store.SavedPreferences?.OfflinePlayerName);
        Assert.Equal(8, service.Current.DownloadConcurrency);
    }

    private sealed class RecordingPreferencesStore(LauncherPreferencesLoadResult loadResult) : ILauncherPreferencesStore
    {
        public LauncherPreferences? SavedPreferences { get; private set; }

        public Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(loadResult);

        public Task SaveAsync(LauncherPreferences preferences, CancellationToken cancellationToken = default)
        {
            SavedPreferences = preferences;
            return Task.CompletedTask;
        }
    }
}
