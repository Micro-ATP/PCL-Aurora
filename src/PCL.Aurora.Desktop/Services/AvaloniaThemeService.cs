using Avalonia.Styling;

namespace PCL.Aurora.Desktop.Services;

/// <summary>
/// 启动器界面的主题选择模式。
/// </summary>
public enum ThemeMode
{
    System,
    Light,
    Dark,
}

/// <summary>
/// 可在界面中选择的主题模式及其可读名称。
/// </summary>
public sealed record ThemeOption(ThemeMode Mode, string DisplayName);

/// <summary>
/// 在当前桌面会话中应用界面主题。
/// </summary>
public interface IThemeService
{
    ThemeMode CurrentMode { get; }

    void Apply(ThemeMode mode);
}

/// <summary>
/// 使用 Avalonia 原生主题变体的桌面主题服务。
/// </summary>
public sealed class AvaloniaThemeService : IThemeService
{
    public ThemeMode CurrentMode { get; private set; } = ThemeMode.System;

    public void Apply(ThemeMode mode)
    {
        var application = Avalonia.Application.Current
            ?? throw new InvalidOperationException("Avalonia 应用尚未初始化，无法切换主题。");

        application.RequestedThemeVariant = mode switch
        {
            ThemeMode.System => ThemeVariant.Default,
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "不支持的主题模式。"),
        };
        CurrentMode = mode;
    }
}
