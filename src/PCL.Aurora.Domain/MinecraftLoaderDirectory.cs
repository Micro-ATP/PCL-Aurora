namespace PCL.Aurora.Domain;

public sealed record MinecraftLoaderDirectory(
    MinecraftLoaderKind Kind,
    string SourceName,
    IReadOnlyList<MinecraftLoaderDirectoryGroup> Groups);

public sealed record MinecraftLoaderDirectoryGroup(
    string Key,
    string Title,
    IReadOnlyList<MinecraftLoaderPackageEntry> Entries,
    bool IsCollapsible = true,
    bool IsLazy = false);

public sealed record MinecraftLoaderPackageEntry(
    MinecraftLoaderKind Kind,
    string MinecraftVersion,
    string Version,
    string DisplayName,
    MinecraftLoaderChannel Channel,
    bool IsRecommended,
    string FileName,
    Uri DownloadUri,
    IReadOnlyList<Uri> AlternativeUris,
    Uri? ChangelogUri,
    string Information,
    long MinimumSize = 65_536);

public sealed record MinecraftLoaderDirectoryResult(
    MinecraftLoaderDirectory? Directory,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Directory is not null && Errors.Count == 0;
}
