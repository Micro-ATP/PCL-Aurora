using PCL.Aurora.Application;
using PCL.Aurora.Infrastructure;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class JsonLauncherPreferencesStoreTests : IDisposable
{
    private readonly string applicationDataDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-preferences-{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadAsync_ReturnsDefaultWithoutCreatingFileWhenPreferencesAreMissing()
    {
        var store = CreateStore();

        var result = await store.LoadAsync();

        Assert.Equal(LauncherThemeMode.System, result.Preferences.ThemeMode);
        Assert.Null(result.Warning);
        Assert.False(File.Exists(GetPreferencesPath()));
    }

    [Fact]
    public async Task SaveAsync_RoundTripsValidatedThemeMode()
    {
        var store = CreateStore();

        await store.SaveAsync(new LauncherPreferences(LauncherThemeMode.Dark));
        var result = await store.LoadAsync();

        Assert.Equal(LauncherThemeMode.Dark, result.Preferences.ThemeMode);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsValidatedDownloadSettings()
    {
        var store = CreateStore();

        await store.SaveAsync(new LauncherPreferences(
            LauncherThemeMode.System,
            DownloadConcurrency: 8,
            DownloadSpeedLimitStep: 31));
        var result = await store.LoadAsync();

        Assert.Equal(8, result.Preferences.DownloadConcurrency);
        Assert.Equal(31, result.Preferences.DownloadSpeedLimitStep);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsSafeSelectedInstanceName()
    {
        var store = CreateStore();

        await store.SaveAsync(new LauncherPreferences(LauncherThemeMode.Light, "fabric-1.21.4"));
        var result = await store.LoadAsync();

        Assert.Equal("fabric-1.21.4", result.Preferences.SelectedInstanceName);
    }

    [Fact]
    public async Task SaveAsync_RejectsInstanceNameContainingPathTraversal()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SaveAsync(new LauncherPreferences(LauncherThemeMode.System, "../outside")));

        Assert.False(File.Exists(GetPreferencesPath()));
    }

    [Fact]
    public async Task SaveAsync_RoundTripsValidatedOfflinePlayerName()
    {
        var store = CreateStore();

        await store.SaveAsync(new LauncherPreferences(LauncherThemeMode.System, OfflinePlayerName: "Aurora_01"));
        var result = await store.LoadAsync();

        Assert.Equal("Aurora_01", result.Preferences.OfflinePlayerName);
    }

    [Fact]
    public async Task SaveAsync_RejectsInvalidOfflinePlayerName()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SaveAsync(new LauncherPreferences(LauncherThemeMode.System, OfflinePlayerName: "not valid")));

        Assert.False(File.Exists(GetPreferencesPath()));
    }

    [Fact]
    public async Task LoadAsync_RecoversFromInvalidPreferencesWithoutOverwritingFile()
    {
        Directory.CreateDirectory(applicationDataDirectory);
        await File.WriteAllTextAsync(GetPreferencesPath(), "{ \"themeMode\": \"unknown\" }");
        var store = CreateStore();

        var result = await store.LoadAsync();

        Assert.Equal(LauncherThemeMode.System, result.Preferences.ThemeMode);
        Assert.NotNull(result.Warning);
        Assert.True(File.Exists(GetPreferencesPath()));
    }

    public void Dispose()
    {
        if (Directory.Exists(applicationDataDirectory))
        {
            Directory.Delete(applicationDataDirectory, recursive: true);
        }
    }

    private JsonLauncherPreferencesStore CreateStore() =>
        new(new FixedPlatformPaths(applicationDataDirectory));

    private string GetPreferencesPath() => Path.Combine(applicationDataDirectory, "preferences.json");

    private sealed class FixedPlatformPaths(string applicationDataDirectory) : IPlatformPaths
    {
        public PlatformPaths Get() => new(applicationDataDirectory, Path.Combine(applicationDataDirectory, "cache"));
    }
}
