using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftVersionCatalogFilterTests
{
    private static readonly IReadOnlyList<MinecraftVersionCatalogEntry> Versions =
    [
        Create("1.21.4", "release", 3),
        Create("24w14a", "snapshot", 2),
        Create("b1.7.3", "old_beta", 1),
    ];

    [Fact]
    public void Filter_GroupsAndOrdersVersionTypes()
    {
        var result = MinecraftVersionCatalogFilter.Filter(Versions, null, includeRelease: true, includeSnapshot: true, includeLegacy: false);

        Assert.Equal(["1.21.4", "24w14a"], result.Select(version => version.Id));
    }

    [Fact]
    public void Filter_MatchesVersionIdWithoutCaseSensitivity()
    {
        var result = MinecraftVersionCatalogFilter.Filter(Versions, "W14A", includeRelease: true, includeSnapshot: true, includeLegacy: true);

        var version = Assert.Single(result);
        Assert.Equal("24w14a", version.Id);
    }

    [Fact]
    public void Filter_ReturnsEmptyWhenNoTypeIsIncluded()
    {
        var result = MinecraftVersionCatalogFilter.Filter(Versions, null, includeRelease: false, includeSnapshot: false, includeLegacy: false);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterByCategory_SeparatesAprilFoolsFromSnapshots()
    {
        var versions = new[]
        {
            Create("25w14craftmine", "snapshot", 4),
            Create("25w15a", "snapshot", 3),
            Create("3D Shareware v1.34", "snapshot", 2),
        };

        var aprilFools = MinecraftVersionCatalogFilter.FilterByCategory(
            versions,
            null,
            MinecraftVersionCatalogCategory.AprilFools);
        var snapshots = MinecraftVersionCatalogFilter.FilterByCategory(
            versions,
            null,
            MinecraftVersionCatalogCategory.Snapshot);

        Assert.Equal(["25w14craftmine", "3D Shareware v1.34"], aprilFools.Select(version => version.Id));
        Assert.Equal("25w15a", Assert.Single(snapshots).Id);
    }

    private static MinecraftVersionCatalogEntry Create(string id, string type, int day) =>
        new(id, type, new Uri($"https://example.invalid/{id}.json"), new DateTimeOffset(2025, 1, day, 0, 0, 0, TimeSpan.Zero));
}
