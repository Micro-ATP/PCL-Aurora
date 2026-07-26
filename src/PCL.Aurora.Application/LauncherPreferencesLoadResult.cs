namespace PCL.Aurora.Application;

/// <summary>
/// 启动器偏好加载结果；文件缺失或无效时返回安全默认值。
/// </summary>
public sealed record LauncherPreferencesLoadResult(LauncherPreferences Preferences, string? Warning);
