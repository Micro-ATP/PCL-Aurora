namespace PCL.Aurora.Domain;

/// <summary>
/// 对已获取的官方 Minecraft 版本清单执行本地筛选和排序。
/// </summary>
public static class MinecraftVersionCatalogFilter
{
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
