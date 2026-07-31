using PCL.Aurora.Application;
using PCL.Aurora.Domain;
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
        Assert.Equal(LauncherLocalizationSettings.Default, result.Preferences.EffectiveLocalizationSettings);
        Assert.Equal(LauncherMiscSettings.Default, result.Preferences.EffectiveMiscSettings);
        Assert.Equal(LauncherUpdateSettings.Default, result.Preferences.EffectiveUpdateSettings);
        Assert.Null(result.Warning);
        Assert.False(File.Exists(GetPreferencesPath()));
    }

    [Fact]
    public async Task SaveAsync_RoundTripsLocalizationAndMiscSettingsWithoutProxyPassword()
    {
        var store = CreateStore();
        var localization = new LauncherLocalizationSettings("en-US", "ui-language");
        var misc = LauncherMiscSettings.Default with
        {
            EnableDoh = false,
            ProxyMode = LauncherProxyMode.Custom,
            CustomProxyAddress = "http://127.0.0.1:7890/",
            CustomProxyUsername = "Aurora",
        };

        await store.SaveAsync(new LauncherPreferences(
            LauncherThemeMode.System,
            LocalizationSettings: localization,
            MiscSettings: misc));
        var result = await store.LoadAsync();

        Assert.Equal(localization, result.Preferences.EffectiveLocalizationSettings);
        Assert.Equal(misc, result.Preferences.EffectiveMiscSettings);
        var storedJson = await File.ReadAllTextAsync(GetPreferencesPath());
        Assert.DoesNotContain("proxyPassword", storedJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_RejectsInvalidLocalizationOrProxySettings()
    {
        var store = CreateStore();
        var invalidLocalization = new LauncherLocalizationSettings("zh-CN", string.Empty);
        var invalidMisc = LauncherMiscSettings.Default with
        {
            CustomProxyAddress = new string('x', LauncherMiscSettings.MaximumProxyAddressLength + 1),
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SaveAsync(
            new LauncherPreferences(LauncherThemeMode.System, LocalizationSettings: invalidLocalization)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SaveAsync(
            new LauncherPreferences(LauncherThemeMode.System, MiscSettings: invalidMisc)));
        Assert.False(File.Exists(GetPreferencesPath()));
    }

    [Fact]
    public async Task LoadAsync_RecoversFromNullProxyFieldsWithoutOverwritingFile()
    {
        Directory.CreateDirectory(applicationDataDirectory);
        await File.WriteAllTextAsync(
            GetPreferencesPath(),
            """{"themeMode":"System","miscSettings":{"customProxyAddress":null,"customProxyUsername":null}}""");
        var store = CreateStore();

        var result = await store.LoadAsync();

        Assert.Equal(LauncherMiscSettings.Default, result.Preferences.EffectiveMiscSettings);
        Assert.NotNull(result.Warning);
        Assert.True(File.Exists(GetPreferencesPath()));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(13, 500)]
    [InlineData(28, 2000)]
    [InlineData(29, int.MaxValue)]
    public void MaximumGameLogLines_FollowsPclCeStepMapping(int step, int expected)
    {
        var settings = LauncherMiscSettings.Default with { MaximumGameLogLinesStep = step };

        Assert.Equal(expected, settings.MaximumGameLogLines);
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
    public async Task SaveAsync_RoundTripsValidatedUpdateSettings()
    {
        var store = CreateStore();
        var settings = new LauncherUpdateSettings(
            LauncherUpdateChannel.Beta,
            LauncherAutoUpdateBehavior.NotifyOnly);

        await store.SaveAsync(new LauncherPreferences(
            LauncherThemeMode.System,
            UpdateSettings: settings));
        var result = await store.LoadAsync();

        Assert.Equal(settings, result.Preferences.EffectiveUpdateSettings);
    }

    [Fact]
    public async Task SaveAsync_RejectsInvalidUpdateSettings()
    {
        var store = CreateStore();
        var settings = new LauncherUpdateSettings(
            (LauncherUpdateChannel)99,
            LauncherAutoUpdateBehavior.DownloadAndNotify);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SaveAsync(new LauncherPreferences(
                LauncherThemeMode.System,
                UpdateSettings: settings)));
    }

    [Fact]
    public async Task LoadAsync_MigratesLegacyDefaultConcurrencyWhenManagementOptionsAreMissing()
    {
        Directory.CreateDirectory(applicationDataDirectory);
        await File.WriteAllTextAsync(
            GetPreferencesPath(),
            """{"themeMode":"System","downloadConcurrency":4,"downloadSpeedLimitStep":42}""");
        var store = CreateStore();

        var result = await store.LoadAsync();

        Assert.Equal(LauncherDownloadSettings.DefaultConcurrency, result.Preferences.DownloadConcurrency);
        Assert.Equal(GameManagementOptions.Default, result.Preferences.EffectiveGameManagementOptions);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsGameManagementOptions()
    {
        var store = CreateStore();
        var options = GameManagementOptions.Default with
        {
            CommunitySource = DownloadSourcePreference.Mirror,
            IgnoreQuilt = false,
            AutoInstallDependencies = false,
        };

        await store.SaveAsync(new LauncherPreferences(
            LauncherThemeMode.System,
            DownloadConcurrency: 4,
            GameManagementOptions: options));
        var result = await store.LoadAsync();

        Assert.Equal(4, result.Preferences.DownloadConcurrency);
        Assert.Equal(options, result.Preferences.EffectiveGameManagementOptions);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsValidatedLaunchOptionsWithoutAnyToken()
    {
        var store = CreateStore();
        var options = new MinecraftLaunchOptions("-Xmx4G", "--demo", MinecraftGameWindowMode.Custom, 1280, 720);

        await store.SaveAsync(new LauncherPreferences(LauncherThemeMode.System, LaunchOptions: options));
        var result = await store.LoadAsync();

        Assert.Equal(options, result.Preferences.EffectiveLaunchOptions);
        var storedJson = await File.ReadAllTextAsync(GetPreferencesPath());
        Assert.DoesNotContain("token", storedJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_RejectsUnsafeLaunchOptions()
    {
        var store = CreateStore();
        var options = new MinecraftLaunchOptions(WindowWidth: 99);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SaveAsync(new LauncherPreferences(LauncherThemeMode.System, LaunchOptions: options)));

        Assert.False(File.Exists(GetPreferencesPath()));
    }

    [Fact]
    public async Task SaveAsync_RoundTripsMicrosoftProfileWithoutAnyToken()
    {
        var store = CreateStore();
        var profile = new MicrosoftAccountProfile("AuroraPlayer", "01234567-89ab-cdef-0123-456789abcdef");

        await store.SaveAsync(new LauncherPreferences(LauncherThemeMode.System, MicrosoftAccount: profile));
        var result = await store.LoadAsync();

        Assert.Equal(profile, result.Preferences.MicrosoftAccount);
        var storedJson = await File.ReadAllTextAsync(GetPreferencesPath());
        Assert.DoesNotContain("token", storedJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_IgnoresLegacyUserConfiguredMicrosoftClientId()
    {
        Directory.CreateDirectory(applicationDataDirectory);
        await File.WriteAllTextAsync(
            GetPreferencesPath(),
            """{"themeMode":"System","microsoftOAuthClientId":"12345678-1234-1234-1234-1234567890ab"}""");
        var store = CreateStore();

        var result = await store.LoadAsync();
        await store.SaveAsync(result.Preferences);

        Assert.Null(result.Warning);
        var storedJson = await File.ReadAllTextAsync(GetPreferencesPath());
        Assert.DoesNotContain("microsoftOAuthClientId", storedJson, StringComparison.OrdinalIgnoreCase);
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
    public async Task SaveAsync_RoundTripsToolboxLaunchCount()
    {
        var store = CreateStore();

        await store.SaveAsync(new LauncherPreferences(LauncherThemeMode.System, LaunchCount: 7));
        var result = await store.LoadAsync();

        Assert.Equal(7, result.Preferences.LaunchCount);
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
