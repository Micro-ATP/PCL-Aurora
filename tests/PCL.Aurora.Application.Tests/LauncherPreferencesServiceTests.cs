using PCL.Aurora.Application;
using PCL.Aurora.Domain;

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

    [Fact]
    public async Task SaveMicrosoftAccountAsync_ChangesOnlySafeProfileMetadata()
    {
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(new LauncherPreferences(LauncherThemeMode.Light, "1.21.4", "Aurora_01"), null));
        var service = new LauncherPreferencesService(store);
        var profile = new MicrosoftAccountProfile("AuroraPlayer", "01234567-89ab-cdef-0123-456789abcdef");
        await service.LoadAsync();

        await service.SaveMicrosoftAccountAsync(profile);

        Assert.Equal(profile, store.SavedPreferences?.MicrosoftAccount);
        Assert.Equal("1.21.4", store.SavedPreferences?.SelectedInstanceName);
        Assert.Equal("Aurora_01", store.SavedPreferences?.OfflinePlayerName);
        await service.SaveMicrosoftAccountAsync(null);
        Assert.Null(store.SavedPreferences?.MicrosoftAccount);
    }

    [Fact]
    public async Task SaveLaunchOptionsAsync_PreservesPreviouslyLoadedPreferences()
    {
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(
                new LauncherPreferences(LauncherThemeMode.Light, "1.21.4", "Aurora_01"),
                null));
        var service = new LauncherPreferencesService(store);
        var options = new MinecraftLaunchOptions("-Xmx4G", "--demo", MinecraftGameWindowMode.Custom, 1280, 720);
        await service.LoadAsync();

        await service.SaveLaunchOptionsAsync(options);

        Assert.Equal(options, store.SavedPreferences?.EffectiveLaunchOptions);
        Assert.Equal("1.21.4", store.SavedPreferences?.SelectedInstanceName);
        Assert.Equal("Aurora_01", store.SavedPreferences?.OfflinePlayerName);
    }

    [Fact]
    public async Task SaveGameManagementOptionsAsync_PreservesPreviouslyLoadedPreferences()
    {
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(
                new LauncherPreferences(LauncherThemeMode.Light, "1.21.4", "Aurora_01"),
                null));
        var service = new LauncherPreferencesService(store);
        var options = GameManagementOptions.Default with { IgnoreQuilt = false };
        await service.LoadAsync();

        await service.SaveGameManagementOptionsAsync(options);

        Assert.Equal(options, store.SavedPreferences?.EffectiveGameManagementOptions);
        Assert.Equal("1.21.4", store.SavedPreferences?.SelectedInstanceName);
        Assert.Equal("Aurora_01", store.SavedPreferences?.OfflinePlayerName);
    }

    [Fact]
    public async Task SaveUpdateSettingsAsync_PreservesPreviouslyLoadedPreferences()
    {
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(
                new LauncherPreferences(LauncherThemeMode.Light, "1.21.4", "Aurora_01"),
                null));
        var service = new LauncherPreferencesService(store);
        var settings = new LauncherUpdateSettings(
            LauncherUpdateChannel.Beta,
            LauncherAutoUpdateBehavior.NotifyOnly);
        await service.LoadAsync();

        await service.SaveUpdateSettingsAsync(settings);

        Assert.Equal(settings, store.SavedPreferences?.EffectiveUpdateSettings);
        Assert.Equal("1.21.4", store.SavedPreferences?.SelectedInstanceName);
        Assert.Equal("Aurora_01", store.SavedPreferences?.OfflinePlayerName);
    }

    [Fact]
    public async Task SaveManualJavaExecutablePathsAsync_PreservesOtherPreferences()
    {
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(
                new LauncherPreferences(LauncherThemeMode.Light, "1.21.4", "Aurora_01"),
                null));
        var service = new LauncherPreferencesService(store);
        var javaPath = Path.GetFullPath(Path.Combine("java", "bin", OperatingSystem.IsWindows() ? "java.exe" : "java"));
        await service.LoadAsync();

        await service.SaveManualJavaExecutablePathsAsync([javaPath]);

        Assert.Equal([javaPath], store.SavedPreferences?.EffectiveManualJavaExecutablePaths);
        Assert.Equal("1.21.4", store.SavedPreferences?.SelectedInstanceName);
        Assert.Equal("Aurora_01", store.SavedPreferences?.OfflinePlayerName);
    }

    [Fact]
    public async Task SaveOfflinePlayerNameAsync_PreservesRecentAccountsAndMovesCurrentToFront()
    {
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(
                new LauncherPreferences(
                    LauncherThemeMode.System,
                    OfflinePlayerName: "Aurora_01",
                    OfflinePlayerNames: ["Aurora_01", "Builder_02"]),
                null));
        var service = new LauncherPreferencesService(store);
        await service.LoadAsync();

        await service.SaveOfflinePlayerNameAsync("Builder_02");

        Assert.Equal("Builder_02", service.Current.OfflinePlayerName);
        Assert.Equal(["Builder_02", "Aurora_01"], service.Current.EffectiveOfflinePlayerNames);
    }

    [Fact]
    public async Task RegisterMinecraftRootDirectoryAsync_NormalizesAndDeduplicatesRoots()
    {
        var firstRoot = Path.GetFullPath(Path.Combine("minecraft", "1.21.4"));
        var secondRoot = Path.GetFullPath(Path.Combine("minecraft", "1.20.1"));
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(
                new LauncherPreferences(
                    LauncherThemeMode.System,
                    MinecraftRootDirectories: [firstRoot]),
                null));
        var service = new LauncherPreferencesService(store);
        await service.LoadAsync();

        await service.RegisterMinecraftRootDirectoryAsync(secondRoot);
        await service.RegisterMinecraftRootDirectoryAsync(firstRoot);

        Assert.Equal([firstRoot, secondRoot], service.Current.EffectiveMinecraftRootDirectories);
    }

    [Fact]
    public async Task SaveOfflineAccountsAsync_RemovesOnlyRequestedHistoryEntry()
    {
        var store = new RecordingPreferencesStore(
            new LauncherPreferencesLoadResult(
                new LauncherPreferences(
                    LauncherThemeMode.System,
                    OfflinePlayerName: "Aurora_01",
                    OfflinePlayerNames: ["Aurora_01", "Builder_02"]),
                null));
        var service = new LauncherPreferencesService(store);
        await service.LoadAsync();

        await service.SaveOfflineAccountsAsync(null, ["Builder_02"]);

        Assert.Null(service.Current.OfflinePlayerName);
        Assert.Equal(["Builder_02"], service.Current.EffectiveOfflinePlayerNames);
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
