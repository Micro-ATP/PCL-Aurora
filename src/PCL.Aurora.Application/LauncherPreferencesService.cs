namespace PCL.Aurora.Application;

/// <summary>
/// 仅公开已明确允许持久化的启动器偏好操作。
/// </summary>
public sealed class LauncherPreferencesService(ILauncherPreferencesStore preferencesStore) : ILauncherPreferencesService
{
    public Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
        preferencesStore.LoadAsync(cancellationToken);

    public Task SaveThemeModeAsync(LauncherThemeMode themeMode, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(themeMode))
        {
            throw new ArgumentOutOfRangeException(nameof(themeMode), themeMode, "不支持的主题模式。");
        }

        return preferencesStore.SaveAsync(new LauncherPreferences(themeMode), cancellationToken);
    }
}
