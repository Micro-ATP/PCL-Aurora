namespace PCL.Aurora.Domain;

public sealed record MinecraftLaunchMetadata(
    string? MainClass,
    IReadOnlyList<string> JvmArguments,
    IReadOnlyList<string> GameArguments,
    bool HasModernArguments,
    bool HasConditionalArguments,
    string? LegacyGameArguments,
    IReadOnlyList<MinecraftConditionalLaunchArgument>? ConditionalJvmArguments = null,
    IReadOnlyList<MinecraftConditionalLaunchArgument>? ConditionalGameArguments = null,
    bool HasUnsupportedConditionalArguments = false);
