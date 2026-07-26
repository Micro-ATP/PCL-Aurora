using Avalonia.Styling;
using PCL.Aurora.Application;

namespace PCL.Aurora.Desktop.Services;

/// <summary>
/// 可在界面中选择的主题模式及其可读名称。
/// </summary>
public sealed record ThemeOption(LauncherThemeMode Mode, string DisplayName);

/// <summary>
/// 在当前桌面会话中应用界面主题。
/// </summary>
public interface IThemeService
{
    LauncherThemeMode CurrentMode { get; }

    void Apply(LauncherThemeMode mode);
}

/// <summary>
/// 使用 Avalonia 原生主题变体的桌面主题服务。
/// </summary>
public sealed class AvaloniaThemeService : IThemeService
{
    public LauncherThemeMode CurrentMode { get; private set; } = LauncherThemeMode.System;

    public void Apply(LauncherThemeMode mode)
    {
        var application = Avalonia.Application.Current
            ?? throw new InvalidOperationException("Avalonia 应用尚未初始化，无法切换主题。");

        application.RequestedThemeVariant = mode switch
        {
            LauncherThemeMode.System => ThemeVariant.Default,
            LauncherThemeMode.Light => ThemeVariant.Light,
            LauncherThemeMode.Dark => ThemeVariant.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "不支持的主题模式。"),
        };
        CurrentMode = mode;
    }
}
