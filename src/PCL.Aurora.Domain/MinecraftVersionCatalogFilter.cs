namespace PCL.Aurora.Domain;

/// <summary>
/// 对已获取的官方 Minecraft 版本清单执行本地筛选和排序。
/// </summary>
public static class MinecraftVersionCatalogFilter
{
    private static readonly HashSet<string> AprilFoolsVersionIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "15w14a",
        "1.RV-Pre1",
        "3D Shareware v1.34",
        "20w14∞",
        "22w13oneblockatatime",
        "23w13a_or_b",
        "24w14potato",
        "25w14craftmine",
        "26w14a",
    };

    public static IReadOnlyList<MinecraftVersionCatalogEntry> Filter(
        IEnumerable<MinecraftVersionCatalogEntry> versions,
        string? searchText,
        bool includeRelease,
        bool includeSnapshot,
        bool includeLegacy)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var query = searchText?.Trim() ?? string.Empty;

        return versions
            .Where(version => IsIncluded(version.Type, includeRelease, includeSnapshot, includeLegacy))
            .Where(version => string.IsNullOrEmpty(query) ||
                              version.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(version => version.ReleaseTime)
            .ToArray();
    }

    /// <summary>
    /// PCL2/PCL-CE-style version grouping. April Fools versions are removed from
    /// their manifest type group so each entry appears in only one section.
    /// </summary>
    public static IReadOnlyList<MinecraftVersionCatalogEntry> FilterByCategory(
        IEnumerable<MinecraftVersionCatalogEntry> versions,
        string? searchText,
        MinecraftVersionCatalogCategory category)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var query = searchText?.Trim() ?? string.Empty;

        return versions
            .Where(version => GetCategory(version) == category)
            .Where(version => string.IsNullOrEmpty(query) ||
                              version.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(version => version.ReleaseTime)
            .ToArray();
    }

    public static MinecraftVersionCatalogCategory GetCategory(MinecraftVersionCatalogEntry version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (IsAprilFoolsVersion(version.Id))
        {
            return MinecraftVersionCatalogCategory.AprilFools;
        }

        return version.Type switch
        {
            "release" => MinecraftVersionCatalogCategory.Release,
            "snapshot" => MinecraftVersionCatalogCategory.Snapshot,
            _ => MinecraftVersionCatalogCategory.Legacy,
        };
    }

    private static bool IsAprilFoolsVersion(string id) =>
        AprilFoolsVersionIds.Contains(id) ||
        id.StartsWith("2.0", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("20w14inf", StringComparison.OrdinalIgnoreCase);

    private static bool IsIncluded(
        string type,
        bool includeRelease,
        bool includeSnapshot,
        bool includeLegacy) => type switch
        {
            "release" => includeRelease,
            "snapshot" => includeSnapshot,
            _ => includeLegacy,
        };
}
