using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class ModrinthCommunityResourceParserTests
{
    [Fact]
    public void Parse_ReadsModrinthSearchHit()
    {
        var result = ModrinthCommunityResourceParser.Parse(
            """
            {
              "hits": [
                {
                  "project_id": "AABBcc12",
                  "project_type": "mod",
                  "slug": "sodium",
                  "author": "jellysquid3",
                  "title": "Sodium",
                  "description": "A rendering optimization mod.",
                  "categories": ["fabric", "optimization"],
                  "display_categories": ["optimization", "fabric"],
                  "versions": ["1.21.1", "1.21"],
                  "downloads": 123456,
                  "follows": 789,
                  "icon_url": "https://cdn.modrinth.com/data/AABBcc12/icon.png",
                  "date_modified": "2026-07-20T10:00:00Z",
                  "latest_version": "version-id"
                }
              ],
              "offset": 20,
              "limit": 20,
              "total_hits": 41
            }
            """,
            CommunityResourceType.Mod);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasNextPage);
        Assert.Equal(20, result.Offset);
        Assert.Equal(41, result.TotalHits);
        var project = Assert.Single(result.Projects);
        Assert.Equal("Sodium", project.Title);
        Assert.Equal("https://modrinth.com/mod/sodium", project.WebsiteUrl.AbsoluteUri.TrimEnd('/'));
        Assert.Equal(123456, project.Downloads);
        Assert.Contains("optimization", project.Categories);
    }

    [Fact]
    public void Parse_PreservesValidHitsAndReportsUnsafeOrMismatchedHits()
    {
        var result = ModrinthCommunityResourceParser.Parse(
            """
            {
              "hits": [
                { "project_id": "ok", "project_type": "shader", "slug": "complementary", "title": "Complementary", "downloads": 2, "follows": 1 },
                { "project_id": "bad", "project_type": "mod", "slug": "../escape", "title": "Bad", "downloads": 0, "follows": 0 }
              ],
              "offset": 0,
              "limit": 20,
              "total_hits": 2
            }
            """,
            CommunityResourceType.Shader);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Projects);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Parse_RecognizesModrinthDataPackCategoryOnModProject()
    {
        var result = ModrinthCommunityResourceParser.Parse(
            """
            {
              "hits": [
                {
                  "project_id": "data-pack-1",
                  "project_type": "mod",
                  "slug": "example-data-pack",
                  "title": "Example Data Pack",
                  "categories": ["datapack", "worldgen"],
                  "downloads": 12,
                  "follows": 3
                }
              ],
              "offset": 0,
              "limit": 20,
              "total_hits": 1
            }
            """,
            CommunityResourceType.DataPack);

        Assert.True(result.IsSuccess);
        var project = Assert.Single(result.Projects);
        Assert.Equal(CommunityResourceType.DataPack, project.Type);
        Assert.Equal("https://modrinth.com/mod/example-data-pack", project.WebsiteUrl.AbsoluteUri.TrimEnd('/'));
    }
}
