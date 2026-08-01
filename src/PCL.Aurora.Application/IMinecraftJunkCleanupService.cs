namespace PCL.Aurora.Application;

public interface IMinecraftJunkCleanupService
{
    Task<MinecraftJunkCleanupPlan> ScanAsync(string minecraftRootDirectory, CancellationToken cancellationToken = default);

    Task<MinecraftJunkCleanupResult> CleanAsync(MinecraftJunkCleanupPlan plan, CancellationToken cancellationToken = default);
}

public sealed record MinecraftJunkCleanupEntry(string Path, bool IsDirectory, int FileCount, long Size);

public sealed record MinecraftJunkCleanupPlan(
    string RootDirectory,
    IReadOnlyList<MinecraftJunkCleanupEntry> Entries,
    int FileCount,
    long TotalSize)
{
    public bool IsEmpty => Entries.Count == 0;
}

public sealed record MinecraftJunkCleanupResult(int DeletedEntries, int DeletedFiles, long DeletedBytes, int FailedEntries);
