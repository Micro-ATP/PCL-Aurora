namespace PCL.Aurora.Domain;

public sealed record MinecraftAssetDownloadPlan(
    string? AssetIndexId,
    IReadOnlyList<MinecraftDownloadArtifact> Artifacts,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => !string.IsNullOrWhiteSpace(AssetIndexId) && BlockingReasons.Count == 0;
}
