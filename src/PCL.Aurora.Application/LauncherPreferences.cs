namespace PCL.Aurora.Application;

/// <summary>
/// 可安全存储在本机的启动器偏好。
/// </summary>
public sealed record LauncherPreferences(LauncherThemeMode ThemeMode)
{
    public static LauncherPreferences Default { get; } = new(LauncherThemeMode.System);

    public bool IsValid => Enum.IsDefined(ThemeMode);
}
