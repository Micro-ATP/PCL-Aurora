namespace PCL.Aurora.Domain;

public sealed record MinecraftNativeLibraryPreparation(
    MinecraftNativeLibraryPlan Plan,
    int ExtractedFileCount,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => Plan.IsReady && BlockingReasons.Count == 0;
}
