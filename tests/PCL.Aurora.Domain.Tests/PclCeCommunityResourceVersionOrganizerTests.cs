using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class PclCeCommunityResourceVersionOrganizerTests
{
    [Fact]
    public void BuildFilters_GroupsLargeCatalogAndOffersLoaderFilters()
    {
        var versions = Enumerable.Range(10, 12)
            .Select(minor => CreateVersion($"v{minor}", $"1.{minor}.1", minor % 2 == 0 ? "fabric" : "forge"))
            .ToArray();

        var filters = PclCeCommunityResourceVersionOrganizer.BuildFilters(versions, CommunityResourceType.Mod);

        Assert.True(filters.GroupByMinorVersion);
        Assert.True(filters.FoldLegacyVersions);
        Assert.Contains(PclCeCommunityResourceVersionOrganizer.LegacyGroup, filters.GameVersions);
        Assert.Contains("Fabric", filters.Loaders);
        Assert.Contains("Forge", filters.Loaders);
    }

    [Fact]
    public void BuildGroups_FiltersLoaderDeduplicatesAndSortsNewestFirst()
    {
        var older = CreateVersion("older", "1.21.1", "fabric", new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = CreateVersion("newer", "1.21.1", "fabric", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var forge = CreateVersion("forge", "1.21.1", "forge", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var versions = new[] { older, newer, forge };
        var filters = PclCeCommunityResourceVersionOrganizer.BuildFilters(versions, CommunityResourceType.Mod);

        var groups = PclCeCommunityResourceVersionOrganizer.BuildGroups(
            versions,
            CommunityResourceType.Mod,
            filters,
            "1.21.1",
            "Fabric");

        var group = Assert.Single(groups);
        Assert.Equal("Fabric 1.21.1", group.Title);
        Assert.Equal(["newer", "older"], group.Versions.Select(version => version.Id));
    }

    [Fact]
    public void BuildGroups_DoesNotIncludeUnknownVersionInSpecificFilter()
    {
        var known = CreateVersion("known", "1.21.1", "fabric");
        var unknown = CreateVersion("unknown", "1.21.1", "fabric") with { GameVersions = [] };
        var versions = new[] { known, unknown };
        var filters = PclCeCommunityResourceVersionOrganizer.BuildFilters(versions, CommunityResourceType.Mod);

        var groups = PclCeCommunityResourceVersionOrganizer.BuildGroups(
            versions,
            CommunityResourceType.Mod,
            filters,
            "1.21.1",
            null);

        Assert.Equal("known", Assert.Single(Assert.Single(groups).Versions).Id);
    }

    private static CommunityResourceVersion CreateVersion(
        string id,
        string gameVersion,
        string loader,
        DateTimeOffset? publishedAt = null) =>
        new(
            id,
            "project",
            id,
            id,
            CommunityResourceVersionChannel.Release,
            publishedAt ?? DateTimeOffset.UtcNow,
            0,
            [gameVersion],
            [loader],
            [new($"{id}.jar", new Uri($"https://cdn.modrinth.com/data/project/versions/{id}/{id}.jar"), new string('a', 40), 1, true)],
            []);
}
