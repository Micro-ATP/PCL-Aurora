using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// 对启动器安全偏好的应用层操作。
/// </summary>
public interface ILauncherPreferencesService
{
    LauncherPreferences Current { get; }

    Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveThemeModeAsync(LauncherThemeMode themeMode, CancellationToken cancellationToken = default);

    Task SaveSelectedInstanceNameAsync(string? instanceName, CancellationToken cancellationToken = default);

    Task SaveOfflinePlayerNameAsync(string? playerName, CancellationToken cancellationToken = default);

    Task SaveOfflineAccountsAsync(
        string? selectedPlayerName,
        IReadOnlyList<string> playerNames,
        CancellationToken cancellationToken = default) =>
        ReplaceAsync(
            Current with
            {
                OfflinePlayerName = selectedPlayerName,
                OfflinePlayerNames = playerNames,
            },
            cancellationToken);

    Task RegisterMinecraftRootDirectoryAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default) =>
        ReplaceAsync(
            Current with
            {
                MinecraftRootDirectories = LauncherPreferences.NormalizeMinecraftRootDirectories(
                    new[] { rootDirectory }.Concat(Current.EffectiveMinecraftRootDirectories)),
            },
            cancellationToken);

    Task SaveDownloadConcurrencyAsync(int concurrency, CancellationToken cancellationToken = default);

    Task SaveDownloadSpeedLimitStepAsync(int speedLimitStep, CancellationToken cancellationToken = default);

    Task SaveGameManagementOptionsAsync(
        GameManagementOptions options,
        CancellationToken cancellationToken = default);

    Task SaveInterfaceSettingsAsync(
        InterfaceSettings settings,
        CancellationToken cancellationToken = default);

    Task SaveLocalizationSettingsAsync(
        LauncherLocalizationSettings settings,
        CancellationToken cancellationToken = default);

    Task SaveMiscSettingsAsync(
        LauncherMiscSettings settings,
        CancellationToken cancellationToken = default);

    Task SaveUpdateSettingsAsync(
        LauncherUpdateSettings settings,
        CancellationToken cancellationToken = default);

    Task SaveManualJavaExecutablePathsAsync(
        IReadOnlyList<string> executablePaths,
        CancellationToken cancellationToken = default);

    Task ReplaceAsync(LauncherPreferences preferences, CancellationToken cancellationToken = default);

    Task SaveMicrosoftAccountAsync(MicrosoftAccountProfile? profile, CancellationToken cancellationToken = default);

    Task SaveLaunchOptionsAsync(MinecraftLaunchOptions options, CancellationToken cancellationToken = default);

    Task SaveLastNotifiedVersionAsync(
        bool snapshot,
        string? version,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
