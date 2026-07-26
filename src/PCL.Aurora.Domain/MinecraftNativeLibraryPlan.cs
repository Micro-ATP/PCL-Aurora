namespace PCL.Aurora.Domain;

public sealed record MinecraftNativeLibraryPlan(
    string NativesDirectory,
    IReadOnlyList<MinecraftNativeLibraryArchive> Archives,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => MissingFiles.Count == 0 && BlockingReasons.Count == 0;
}
