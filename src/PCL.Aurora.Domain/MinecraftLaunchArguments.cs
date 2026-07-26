namespace PCL.Aurora.Domain;

public sealed record MinecraftLaunchArguments(
    IReadOnlyList<string> JvmArguments,
    string MainClass,
    IReadOnlyList<string> GameArguments);
