using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class CurseForgeCommunityResourceParserTests
{
    [Fact]
    public void Parse_ReadsWorldProjectAndPagination()
    {
        var result = CurseForgeCommunityResourceParser.Parse(
            """
            {
              "data": [{
                "id": 307740,
                "slug": "oneblock",
                "name": "Oneblock",
                "summary": "A survival map",
                "downloadCount": 9956415,
                "dateModified": "2026-06-29T11:44:33Z",
                "authors": [{"name":"Crimson Creations"}],
                "logo": {"thumbnailUrl":"https://media.forgecdn.net/avatars/thumbnails/1/2/icon.png"},
                "links": {"websiteUrl":"https://www.curseforge.com/minecraft/worlds/oneblock"},
                "categories": [{"slug":"adventure"},{"slug":"survival"}],
                "latestFilesIndexes": [{"gameVersion":"1.21.1","filename":"Oneblock.zip"}]
              }],
              "pagination": {"index":20,"pageSize":20,"totalCount":6011}
            }
            """);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasNextPage);
        Assert.Equal(6011, result.TotalHits);
        var project = Assert.Single(result.Projects);
        Assert.Equal(CommunityResourceType.World, project.Type);
        Assert.Equal("CurseForge", project.SourceDisplay);
        Assert.Equal("Crimson Creations", project.Author);
        Assert.Contains("生存", project.CategoryTags);
        Assert.Equal(["1.21.1"], project.GameVersions);
    }

    [Fact]
    public void ParseCatalog_ReadsVerifiedCurseForgeFile()
    {
        var catalog = CurseForgeCommunityResourceVersionParser.ParseCatalog(
            """
            {
              "data": [{
                "id": 8340194,
                "modId": 307740,
                "displayName": "Oneblock 4.3.7",
                "fileName": "Oneblock 4.3.7.zip",
                "releaseType": 1,
                "fileDate": "2026-06-29T11:39:13Z",
                "downloadCount": 50,
                "fileLength": 1234,
                "downloadUrl": "https://edge.forgecdn.net/files/8340/194/Oneblock%204.3.7.zip",
                "hashes": [{"algo":1,"value":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}],
                "gameVersions": ["1.21.1"]
              }]
            }
            """);

        Assert.True(catalog.IsSuccess);
        var version = Assert.Single(catalog.Versions);
        Assert.Equal("8340194", version.Id);
        Assert.Equal(CommunityResourceVersionChannel.Release, version.Channel);
        Assert.Equal(["1.21.1"], version.GameVersions);
        Assert.Equal("Oneblock 4.3.7.zip", version.PrimaryFile!.FileName);
    }
}
