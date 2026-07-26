namespace PCL.Aurora.Domain;

public sealed record MinecraftLaunchMetadata(
    string? MainClass,
    IReadOnlyList<string> JvmArguments,
    IReadOnlyList<string> GameArguments,
    bool HasModernArguments,
    bool HasConditionalArguments,
    string? LegacyGameArguments);
