using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftVersionCatalogTests
{
    [Fact]
    public void Parse_ReadsOfficialStyleCatalogAndRejectsUnsafeVersionIds()
    {
        var result = MinecraftVersionCatalogParser.Parse(
            """
            {
              "latest": { "release": "1.21.4", "snapshot": "25w01a" },
              "versions": [
                { "id": "1.21.4", "type": "release", "url": "https://example.invalid/1.21.4.json", "releaseTime": "2024-12-03T00:00:00Z" }
              ]
            }
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.21.4", result.Catalog!.LatestRelease);
        Assert.Equal("1.21.4", Assert.Single(result.Catalog.Versions).Id);
    }
}
