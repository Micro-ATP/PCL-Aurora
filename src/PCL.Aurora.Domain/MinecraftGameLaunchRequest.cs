namespace PCL.Aurora.Domain;

public sealed record MinecraftGameLaunchRequest(
    string JavaExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> ArgumentList,
    IReadOnlyDictionary<string, string> EnvironmentVariables);
