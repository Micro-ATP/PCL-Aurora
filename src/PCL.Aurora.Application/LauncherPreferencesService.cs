namespace PCL.Aurora.Application;

/// <summary>
/// 仅公开已明确允许持久化的启动器偏好操作。
/// </summary>
public sealed class LauncherPreferencesService(ILauncherPreferencesStore preferencesStore) : ILauncherPreferencesService
{
    private readonly SemaphoreSlim updateLock = new(1, 1);
    private LauncherPreferences currentPreferences = LauncherPreferences.Default;

    public async Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            currentPreferences = result.Preferences;
            return result;
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
}
