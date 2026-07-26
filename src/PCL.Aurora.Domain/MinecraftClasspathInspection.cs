namespace PCL.Aurora.Domain;

public sealed record MinecraftClasspathInspection(
    IReadOnlyList<string> Entries,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => Entries.Count > 0 && MissingFiles.Count == 0 && BlockingReasons.Count == 0;

    public string? Value => IsReady ? string.Join(Path.PathSeparator, Entries) : null;
}
