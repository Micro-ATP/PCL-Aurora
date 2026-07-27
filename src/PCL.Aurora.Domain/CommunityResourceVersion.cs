using System.Globalization;

namespace PCL.Aurora.Domain;

public sealed record CommunityResourceVersion(
    string Id,
    string ProjectId,
    string Name,
    string VersionNumber,
    CommunityResourceVersionChannel Channel,
    DateTimeOffset? PublishedAt,
    long Downloads,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<string> Loaders,
    IReadOnlyList<CommunityResourceVersionFile> Files,
    IReadOnlyList<CommunityResourceDependency> Dependencies)
{
    public CommunityResourceVersionFile? PrimaryFile =>
        Files.FirstOrDefault(file => file.IsPrimary) ?? Files.FirstOrDefault();

    public string ChannelDisplay => Channel switch
    {
        CommunityResourceVersionChannel.Release => "正式版",
        CommunityResourceVersionChannel.Beta => "测试版",
        CommunityResourceVersionChannel.Alpha => "开发版",
        _ => "未知",
    };

    public string PublishedAtDisplay =>
        PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) ?? "未知日期";

    public string LoaderSummary => Loaders.Count == 0 ? "通用" : string.Join(" · ", Loaders);

    public string GameVersionSummary => GameVersions.Count == 0
        ? "未标注游戏版本"
        : string.Join(" · ", GameVersions.Take(4));

    public string DependencySummary
    {
        get
        {
            var required = Dependencies.Count(item => item.Type == CommunityResourceDependencyType.Required);
            var optional = Dependencies.Count(item => item.Type == CommunityResourceDependencyType.Optional);
            return (required, optional) switch
            {
                (0, 0) => "无外部依赖",
                (_, 0) => $"{required} 项必要依赖",
                (0, _) => $"{optional} 项可选依赖",
                _ => $"{required} 项必要依赖 · {optional} 项可选依赖",
            };
        }
    }

    public string FileSummary => PrimaryFile is { } file
        ? $"{file.FileName} · {FormatSize(file.Size)}"
        : "没有可下载文件";

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024L * 1024L => $"{bytes / (1024d * 1024d * 1024d):0.##} GiB",
        >= 1024L * 1024L => $"{bytes / (1024d * 1024d):0.##} MiB",
        >= 1024L => $"{bytes / 1024d:0.##} KiB",
        _ => $"{bytes} B",
    };
}
