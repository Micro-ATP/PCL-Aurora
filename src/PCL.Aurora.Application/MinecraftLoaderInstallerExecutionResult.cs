namespace PCL.Aurora.Application;

public sealed record MinecraftLoaderInstallerExecutionResult(
    int? ExitCode,
    IReadOnlyList<GameProcessOutput> Output,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => ExitCode == 0 && Errors.Count == 0;
}
