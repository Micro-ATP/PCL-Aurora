namespace PCL.Aurora.Domain;

public sealed record MinecraftGameLaunchRequest(
    string JavaExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> ArgumentList,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    string? PreLaunchCommand = null,
    bool WaitForPreLaunchCommand = true,
    MinecraftGameProcessPriority ProcessPriority = MinecraftGameProcessPriority.Normal,
    int MainClassArgumentIndex = -1,
    bool PreferDedicatedGpu = false);
