namespace PCL.Aurora.Domain;

public sealed record MinecraftInstance(
    string Name,
    string DirectoryPath,
    string? VersionId,
    string? Type,
    DateTimeOffset? ReleaseTime,
    MinecraftInstanceStatus Status,
    string? InheritsFrom = null,
    string? BaseVersionId = null,
    MinecraftInstalledLoader? InstalledLoader = null)
{
    public string VersionDisplay => string.IsNullOrWhiteSpace(BaseVersionId) ||
                                    string.Equals(BaseVersionId, VersionId, StringComparison.OrdinalIgnoreCase)
        ? VersionId ?? "未知版本"
        : $"{BaseVersionId}（派生版本：{VersionId ?? Name}）";

    public string LoaderDisplay => InstalledLoader is null
        ? "原版或未识别的加载器"
        : $"{InstalledLoader.Kind} {InstalledLoader.Version ?? "未知版本"}";
}
