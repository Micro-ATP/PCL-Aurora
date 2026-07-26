namespace PCL.Aurora.Application;

/// <summary>
/// 启动器偏好的跨平台存储边界。
/// </summary>
public interface ILauncherPreferencesStore
{
    Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LauncherPreferences preferences, CancellationToken cancellationToken = default);
}
