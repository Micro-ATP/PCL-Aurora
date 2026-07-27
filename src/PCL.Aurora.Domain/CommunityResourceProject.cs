using System.Globalization;

namespace PCL.Aurora.Domain;

public sealed record CommunityResourceProject(
    string Id,
    string Slug,
    string Title,
    string Description,
    string Author,
    CommunityResourceType Type,
    Uri WebsiteUrl,
    Uri? IconUrl,
    long Downloads,
    long Followers,
    DateTimeOffset? LastUpdated,
    string? LatestVersion,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> GameVersions)
{
    public string Initial => string.IsNullOrWhiteSpace(Title)
        ? "?"
        : StringInfo.GetNextTextElement(Title.Trim()).ToUpper(CultureInfo.CurrentCulture);

    public string DownloadCountDisplay => Downloads.ToString("N0", CultureInfo.CurrentCulture);

    public string FollowerCountDisplay => Followers.ToString("N0", CultureInfo.CurrentCulture);

    public string LastUpdatedDisplay => LastUpdated?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) ?? "未知";

    public string CategorySummary => Categories.Count == 0 ? "未标注分类" : string.Join(" · ", Categories.Take(4));

    public string GameVersionSummary => GameVersions.Count == 0
        ? "未标注游戏版本"
        : string.Join(" · ", GameVersions.Take(4));
}
