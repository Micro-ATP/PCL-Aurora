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

    private static MinecraftVersionCatalogEntry Create(string id, string type, int day) =>
        new(id, type, new Uri($"https://example.invalid/{id}.json"), new DateTimeOffset(2025, 1, day, 0, 0, 0, TimeSpan.Zero));
}
