namespace PCL.Aurora.Domain;

public sealed record MinecraftAssetMappingPreparation(
    MinecraftAssetMappingPlan Plan,
    int MappedFileCount,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => Plan.IsReady && BlockingReasons.Count == 0;
}
