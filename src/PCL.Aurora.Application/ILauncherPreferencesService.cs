namespace PCL.Aurora.Application;

/// <summary>
/// 对启动器安全偏好的应用层操作。
/// </summary>
public interface ILauncherPreferencesService
{
    Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveThemeModeAsync(LauncherThemeMode themeMode, CancellationToken cancellationToken = default);

    Task SaveSelectedInstanceNameAsync(string? instanceName, CancellationToken cancellationToken = default);

    Task SaveOfflinePlayerNameAsync(string? playerName, CancellationToken cancellationToken = default);
}
