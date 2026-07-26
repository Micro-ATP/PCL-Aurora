namespace PCL.Aurora.Domain;

/// <summary>
/// 用户明确配置且可安全持久化的全局 Minecraft 启动选项。
/// </summary>
public sealed record MinecraftLaunchOptions(
    string? AdditionalJvmArguments = null,
    string? AdditionalGameArguments = null,
    MinecraftGameWindowMode WindowMode = MinecraftGameWindowMode.Default,
    int WindowWidth = 854,
    int WindowHeight = 480)
{
    public const int DefaultWindowWidth = 854;

    public const int DefaultWindowHeight = 480;

    public const int MinimumWindowDimension = 100;

    public const int MaximumWindowDimension = 99999;

    public const int MaximumArgumentTextLength = 4000;

    public static MinecraftLaunchOptions Default { get; } = new();

    public bool IsValid =>
        IsValidArgumentText(AdditionalJvmArguments) &&
        IsValidArgumentText(AdditionalGameArguments) &&
        Enum.IsDefined(WindowMode) &&
        IsValidWindowDimension(WindowWidth) &&
        IsValidWindowDimension(WindowHeight);

    public static bool IsValidArgumentText(string? value) =>
        value is null || value.Length <= MaximumArgumentTextLength;

    public static bool IsValidWindowDimension(int value) =>
        value >= MinimumWindowDimension && value <= MaximumWindowDimension;
}
