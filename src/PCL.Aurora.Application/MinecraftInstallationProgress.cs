namespace PCL.Aurora.Application;

public sealed record MinecraftInstallationProgress(
    int CompletedStages,
    int TotalStages,
    string Description,
    int CompletedArtifacts = 0,
    int TotalArtifacts = 0,
    int ActiveArtifacts = 0,
    long DownloadedBytes = 0,
    long? TotalBytes = null);
