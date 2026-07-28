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

    Task SaveDownloadConcurrencyAsync(int concurrency, CancellationToken cancellationToken = default);

    Task SaveDownloadSpeedLimitStepAsync(int speedLimitStep, CancellationToken cancellationToken = default);

    Task SaveMicrosoftAccountAsync(MicrosoftAccountProfile? profile, CancellationToken cancellationToken = default);

    Task SaveMicrosoftOAuthClientIdAsync(string? clientId, CancellationToken cancellationToken = default);

    Task SaveLaunchOptionsAsync(MinecraftLaunchOptions options, CancellationToken cancellationToken = default);
}
