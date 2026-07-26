namespace PCL.Aurora.Domain;

public sealed record MinecraftDownloadPlan(
    string? VersionId,
    IReadOnlyList<MinecraftDownloadArtifact> Artifacts,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => !string.IsNullOrWhiteSpace(VersionId) && BlockingReasons.Count == 0;
}
