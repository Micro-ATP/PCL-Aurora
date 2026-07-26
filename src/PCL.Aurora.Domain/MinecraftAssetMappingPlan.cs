namespace PCL.Aurora.Domain;

public sealed record MinecraftAssetMappingPlan(
    string? TargetDirectory,
    IReadOnlyList<MinecraftAssetMappingEntry> Entries,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => MissingFiles.Count == 0 && BlockingReasons.Count == 0;
}
