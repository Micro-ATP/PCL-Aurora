namespace PCL.Aurora.Domain;

/// <summary>
/// 用户明确配置且可安全持久化的全局 Minecraft 启动选项。
/// </summary>
public sealed record MinecraftLaunchOptions(
    string? AdditionalJvmArguments = MinecraftLaunchOptions.DefaultAdditionalJvmArguments,
    string? AdditionalGameArguments = null,
    MinecraftGameWindowMode WindowMode = MinecraftGameWindowMode.Default,
    int WindowWidth = 854,
    int WindowHeight = 480,
    MinecraftMemoryAllocationMode MemoryAllocationMode = MinecraftMemoryAllocationMode.Automatic,
    int CustomMemoryMiB = 3072)
{
    public const string DefaultAdditionalJvmArguments =
        "-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow " +
        "-Djdk.lang.Process.allowAmbiguousCommands=true " +
        "-Dfml.ignoreInvalidMinecraftCertificates=True " +
        "-Dfml.ignorePatchDiscrepancies=True " +
        "-Dlog4j2.formatMsgNoLookups=true";

    public const int DefaultWindowWidth = 854;

    public const int DefaultWindowHeight = 480;

    public const int MinimumWindowDimension = 100;

    public const int MaximumWindowDimension = 99999;

    public const int MaximumArgumentTextLength = 4000;

    public const int DefaultCustomMemoryMiB = 3072;

    public const int MinimumCustomMemoryMiB = 256;

    public const int MaximumCustomMemoryMiB = 262144;

    public static MinecraftLaunchOptions Default { get; } = new();

    public bool IsValid =>
        IsValidArgumentText(AdditionalJvmArguments) &&
        IsValidArgumentText(AdditionalGameArguments) &&
        Enum.IsDefined(WindowMode) &&
        IsValidWindowDimension(WindowWidth) &&
        IsValidWindowDimension(WindowHeight) &&
        Enum.IsDefined(MemoryAllocationMode) &&
        IsValidCustomMemoryMiB(CustomMemoryMiB);

    public static bool IsValidArgumentText(string? value) =>
        value is null || value.Length <= MaximumArgumentTextLength;

    public static bool IsValidWindowDimension(int value) =>
        value >= MinimumWindowDimension && value <= MaximumWindowDimension;

    public static bool IsValidCustomMemoryMiB(int value) =>
        value >= MinimumCustomMemoryMiB && value <= MaximumCustomMemoryMiB;
}
