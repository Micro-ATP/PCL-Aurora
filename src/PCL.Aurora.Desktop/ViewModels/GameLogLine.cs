using PCL.Aurora.Application;

namespace PCL.Aurora.Desktop.ViewModels;

/// <summary>
/// 当前游戏进程会话的一行输出，供桌面端只读呈现。
/// </summary>
public sealed record GameLogLine(bool IsError, string Text)
{
    public string DisplayText => $"{(IsError ? "stderr" : "stdout")} | {Text}";

    public static GameLogLine FromOutput(GameProcessOutput output) => new(output.IsError, output.Text);
}
