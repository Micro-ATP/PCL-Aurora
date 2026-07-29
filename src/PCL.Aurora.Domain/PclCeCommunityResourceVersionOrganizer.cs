// Directly adapts the filter and card-grouping rules from PCL-CE
// Plain Craft Launcher 2/Pages/PageDownload/Comp/PageDownloadCompDetail.xaml.cs.
// Modified by Micro-ATP for immutable cross-platform domain models.
// See LICENSES/PCL-CE-Plain-Craft-Launcher-2-LICENCE.txt and NOTICE.

namespace PCL.Aurora.Domain;

public static class PclCeCommunityResourceVersionOrganizer
{
    public const string AllFilter = "全部";
    public const string PreviewGroup = "预览版";
    public const string LegacyGroup = "远古版本";
    public const string OtherGroup = "其他版本";

    private static readonly IReadOnlyDictionary<string, string> LoaderDisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["forge"] = "Forge",
            ["neoforge"] = "NeoForge",
            ["fabric"] = "Fabric",
            ["quilt"] = "Quilt",
        };

    public static CommunityResourceVersionFilterSet BuildFilters(
        IReadOnlyList<CommunityResourceVersion> versions,
        CommunityResourceType type)
    {
        ArgumentNullException.ThrowIfNull(versions);

        var groupByMinor = false;
        var foldLegacy = false;
        var gameVersions = BuildGameVersionFilters(versions, groupByMinor, foldLegacy);
        if (gameVersions.Count >= 9)
        {
            groupByMinor = true;
            gameVersions = BuildGameVersionFilters(versions, groupByMinor, foldLegacy);
            if (gameVersions.Count >= 9)
            {
                groupByMinor = false;
                foldLegacy = true;
                gameVersions = BuildGameVersionFilters(versions, groupByMinor, foldLegacy);
                if (gameVersions.Count >= 9)
                {
                    groupByMinor = true;
                    gameVersions = BuildGameVersionFilters(versions, groupByMinor, foldLegacy);
                }
            }
        }

        var loaders = type == CommunityResourceType.Mod
            ? versions
                .SelectMany(version => version.Loaders)
                .Where(LoaderDisplayNames.ContainsKey)
                .Select(GetLoaderDisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        return new(gameVersions, loaders, groupByMinor, foldLegacy);
    }

    public static IReadOnlyList<CommunityResourceVersionGroup> BuildGroups(
        IReadOnlyList<CommunityResourceVersion> versions,
        CommunityResourceType type,
        CommunityResourceVersionFilterSet filters,
        string? gameVersionFilter,
        string? loaderFilter)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(filters);

        var splitByLoader = type == CommunityResourceType.Mod && filters.Loaders.Count > 1;
        var groups = new Dictionary<string, Dictionary<string, CommunityResourceVersion>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var version in versions)
        {
            var matchingGameVersions = version.GameVersions.Count == 0
                ? string.IsNullOrWhiteSpace(gameVersionFilter) ||
                  string.Equals(gameVersionFilter, OtherGroup, StringComparison.OrdinalIgnoreCase)
                    ? new[] { OtherGroup }
                    : []
                : version.GameVersions
                    .Where(gameVersion => MatchesGameVersionFilter(gameVersion, filters, gameVersionFilter))
                    .ToArray();
            if (matchingGameVersions.Length == 0)
            {
                continue;
            }

            var loaders = version.Loaders
                .Where(LoaderDisplayNames.ContainsKey)
                .Select(GetLoaderDisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (!string.IsNullOrWhiteSpace(loaderFilter))
            {
                loaders = loaders
                    .Where(loader => string.Equals(loader, loaderFilter, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (loaders.Length == 0)
                {
                    continue;
                }
            }

            if (!splitByLoader || loaders.Length == 0)
            {
                loaders = [string.Empty];
            }

            foreach (var gameVersion in matchingGameVersions)
            {
                var exactGroup = GetExactGroupName(gameVersion);
                foreach (var loader in loaders)
                {
                    var title = string.IsNullOrEmpty(loader) ? exactGroup : $"{loader} {exactGroup}";
                    if (!groups.TryGetValue(title, out var groupVersions))
                    {
                        groupVersions = new(StringComparer.OrdinalIgnoreCase);
                        groups.Add(title, groupVersions);
                    }

                    groupVersions.TryAdd(version.Id, version);
                }
            }
        }

        return groups
            .Select(pair => new CommunityResourceVersionGroup(
                pair.Key,
                pair.Value.Values
                    .OrderByDescending(version => version.PublishedAt)
                    .ThenByDescending(version => version.VersionNumber, new PclCeVersionComparer.VersionComparer())
                    .ToArray()))
            .OrderBy(group => IsSpecialGroup(group.Title))
            .ThenByDescending(group => StripLoaderPrefix(group.Title), new PclCeVersionComparer.VersionComparer())
            .ThenBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string GetFilterGroupName(
        string? gameVersion,
        bool groupByMinorVersion,
        bool foldLegacyVersions)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            return OtherGroup;
        }

        if (IsPreviewVersion(gameVersion))
        {
            return PreviewGroup;
        }

        if (!TryParseReleaseVersion(gameVersion, out var major, out var minor))
        {
            return OtherGroup;
        }

        if (foldLegacyVersions && major == 1 && minor < 12)
        {
            return LegacyGroup;
        }

        return groupByMinorVersion ? $"{major}.{minor}" : gameVersion.Trim();
    }

    public static string GetLoaderDisplayName(string loader) =>
        LoaderDisplayNames.GetValueOrDefault(loader.Trim(), loader.Trim());

    private static IReadOnlyList<string> BuildGameVersionFilters(
        IReadOnlyList<CommunityResourceVersion> versions,
        bool groupByMinor,
        bool foldLegacy) =>
        versions
            .SelectMany(version => version.GameVersions.Count == 0 ? [OtherGroup] : version.GameVersions)
            .Select(version => GetFilterGroupName(version, groupByMinor, foldLegacy))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => IsSpecialGroup(value))
            .ThenByDescending(value => value, new PclCeVersionComparer.VersionComparer())
            .ToArray();

    private static bool MatchesGameVersionFilter(
        string gameVersion,
        CommunityResourceVersionFilterSet filters,
        string? selectedFilter) =>
        string.IsNullOrWhiteSpace(selectedFilter) ||
        string.Equals(
            GetFilterGroupName(gameVersion, filters.GroupByMinorVersion, filters.FoldLegacyVersions),
            selectedFilter,
            StringComparison.OrdinalIgnoreCase);

    private static string GetExactGroupName(string gameVersion) =>
        gameVersion is OtherGroup
            ? OtherGroup
            : GetFilterGroupName(gameVersion, groupByMinorVersion: false, foldLegacyVersions: false);

    private static bool IsSpecialGroup(string value) =>
        value.Contains(PreviewGroup, StringComparison.Ordinal) ||
        value.Contains(LegacyGroup, StringComparison.Ordinal) ||
        value.Contains(OtherGroup, StringComparison.Ordinal);

    private static string StripLoaderPrefix(string value)
    {
        var separator = value.IndexOf(' ');
        return separator > 0 && LoaderDisplayNames.Values.Contains(value[..separator], StringComparer.OrdinalIgnoreCase)
            ? value[(separator + 1)..]
            : value;
    }

    private static bool IsPreviewVersion(string value) =>
        value.Contains('w', StringComparison.OrdinalIgnoreCase) ||
        value.Contains("snapshot", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("预览版", StringComparison.Ordinal);

    private static bool TryParseReleaseVersion(string value, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        var parts = value.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 &&
               int.TryParse(parts[0], out major) &&
               int.TryParse(parts[1], out minor);
    }
}
