using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// 仅公开已明确允许持久化的启动器偏好操作。
/// </summary>
public sealed class LauncherPreferencesService(ILauncherPreferencesStore preferencesStore) : ILauncherPreferencesService
{
    private readonly SemaphoreSlim updateLock = new(1, 1);
    private LauncherPreferences currentPreferences = LauncherPreferences.Default;

    public LauncherPreferences Current => Volatile.Read(ref currentPreferences);

    public async Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            currentPreferences = Normalize(result.Preferences);
            return result with { Preferences = currentPreferences };
        }
        finally
        {
            updateLock.Release();
        }
    }

    public Task SaveThemeModeAsync(LauncherThemeMode themeMode, CancellationToken cancellationToken = default) =>
        UpdateAsync(preferences => preferences with { ThemeMode = themeMode }, cancellationToken);

    public Task SaveSelectedInstanceNameAsync(string? instanceName, CancellationToken cancellationToken = default) =>
        UpdateAsync(preferences => preferences with { SelectedInstanceName = instanceName }, cancellationToken);

    public Task SaveOfflinePlayerNameAsync(string? playerName, CancellationToken cancellationToken = default) =>
        UpdateAsync(
            preferences => preferences with
            {
                OfflinePlayerName = playerName,
                OfflinePlayerNames = LauncherPreferences.NormalizeOfflinePlayerNames(
                    playerName,
                    preferences.EffectiveOfflinePlayerNames),
            },
            cancellationToken);

    public Task SaveOfflineAccountsAsync(
        string? selectedPlayerName,
        IReadOnlyList<string> playerNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerNames);
        return UpdateAsync(
            preferences => preferences with
            {
                OfflinePlayerName = selectedPlayerName,
                OfflinePlayerNames = LauncherPreferences.NormalizeOfflinePlayerNames(selectedPlayerName, playerNames),
            },
            cancellationToken);
    }

    public Task RegisterMinecraftRootDirectoryAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return UpdateAsync(
            preferences => preferences with
            {
                MinecraftRootDirectories = LauncherPreferences.NormalizeMinecraftRootDirectories(
                    new[] { rootDirectory }.Concat(preferences.EffectiveMinecraftRootDirectories)),
            },
            cancellationToken);
    }

    public Task SaveDownloadConcurrencyAsync(int concurrency, CancellationToken cancellationToken = default) =>
        UpdateAsync(preferences => preferences with { DownloadConcurrency = concurrency }, cancellationToken);

    public Task SaveDownloadSpeedLimitStepAsync(int speedLimitStep, CancellationToken cancellationToken = default) =>
        UpdateAsync(preferences => preferences with { DownloadSpeedLimitStep = speedLimitStep }, cancellationToken);

    public Task SaveGameManagementOptionsAsync(
        GameManagementOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return UpdateAsync(preferences => preferences with { GameManagementOptions = options }, cancellationToken);
    }

    public Task SaveInterfaceSettingsAsync(
        InterfaceSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return UpdateAsync(preferences => preferences with { InterfaceSettings = settings }, cancellationToken);
    }

    public Task SaveLocalizationSettingsAsync(
        LauncherLocalizationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return UpdateAsync(preferences => preferences with { LocalizationSettings = settings }, cancellationToken);
    }

    public Task SaveMiscSettingsAsync(
        LauncherMiscSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return UpdateAsync(preferences => preferences with { MiscSettings = settings }, cancellationToken);
    }

    public Task SaveUpdateSettingsAsync(
        LauncherUpdateSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(settings));
        }

        return UpdateAsync(preferences => preferences with { UpdateSettings = settings }, cancellationToken);
    }

    public Task SaveManualJavaExecutablePathsAsync(
        IReadOnlyList<string> executablePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executablePaths);
        return UpdateAsync(
            preferences => preferences with { ManualJavaExecutablePaths = executablePaths.ToArray() },
            cancellationToken);
    }

    public Task ReplaceAsync(LauncherPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return UpdateAsync(_ => preferences, cancellationToken);
    }

    public Task SaveMicrosoftAccountAsync(MicrosoftAccountProfile? profile, CancellationToken cancellationToken = default) =>
        UpdateAsync(preferences => preferences with { MicrosoftAccount = profile }, cancellationToken);

    public Task SaveLaunchOptionsAsync(MinecraftLaunchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return UpdateAsync(preferences => preferences with { LaunchOptions = options }, cancellationToken);
    }

    public Task SaveLastNotifiedVersionAsync(
        bool snapshot,
        string? version,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            preferences => snapshot
                ? preferences with { LastNotifiedSnapshotVersion = version }
                : preferences with { LastNotifiedReleaseVersion = version },
            cancellationToken);

    private async Task UpdateAsync(
        Func<LauncherPreferences, LauncherPreferences> update,
        CancellationToken cancellationToken)
    {
        await updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var preferences = update(currentPreferences);
            if (!preferences.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(preferences), "启动器偏好包含不支持的值。");
            }

            currentPreferences = preferences;
            await preferencesStore.SaveAsync(currentPreferences, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            updateLock.Release();
        }
    }

    private static LauncherPreferences Normalize(LauncherPreferences preferences) => preferences with
    {
        OfflinePlayerNames = LauncherPreferences.NormalizeOfflinePlayerNames(
            preferences.OfflinePlayerName,
            preferences.OfflinePlayerNames),
        MinecraftRootDirectories = LauncherPreferences.NormalizeMinecraftRootDirectories(
            preferences.MinecraftRootDirectories),
    };
}
