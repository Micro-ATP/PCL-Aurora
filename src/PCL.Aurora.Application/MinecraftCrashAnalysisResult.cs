namespace PCL.Aurora.Application;

public sealed record MinecraftCrashAnalysisResult(
    string Summary,
    IReadOnlyList<string> Evidence);
