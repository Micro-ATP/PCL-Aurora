using PCL.Aurora.Application;

namespace PCL.Aurora.Application.Tests;

public sealed class LauncherPreferencesServiceTests
{
    [Fact]
    public async Task SaveThemeModeAsync_PreservesLoadedInstanceSelection()
    {
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(new LauncherPreferences(LauncherThemeMode.System, "1.21.4"), null));
        var service = new LauncherPreferencesService(store);
        await service.LoadAsync();

        await service.SaveThemeModeAsync(LauncherThemeMode.Dark);

        Assert.Equal(LauncherThemeMode.Dark, store.SavedPreferences?.ThemeMode);
        Assert.Equal("1.21.4", store.SavedPreferences?.SelectedInstanceName);
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
