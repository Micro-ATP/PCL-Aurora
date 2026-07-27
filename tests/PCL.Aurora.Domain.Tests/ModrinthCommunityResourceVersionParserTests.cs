using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class ModrinthCommunityResourceVersionParserTests
{
    [Fact]
    public void ParseCatalog_ReadsPrimaryFileAndDependencies()
    {
        var result = ModrinthCommunityResourceVersionParser.ParseCatalog(
            """
            [{
              "id":"version-a","project_id":"project-a","name":"Fabric 1.0","version_number":"1.0.0",
              "version_type":"release","date_published":"2026-07-27T08:00:00Z","downloads":42,
              "game_versions":["1.21.1"],"loaders":["fabric"],
              "files":[{"filename":"example.jar","primary":true,"size":12,
                "url":"https://cdn.modrinth.com/data/project-a/versions/version-a/example.jar",
                "hashes":{"sha1":"0123456789abcdef0123456789abcdef01234567"}}],
              "dependencies":[
                {"project_id":"dependency-a","dependency_type":"required"},
                {"version_id":"optional-version","dependency_type":"optional"}
              ]
            }]
            """);

        var version = Assert.Single(result.Versions);
        Assert.True(result.IsSuccess);
        Assert.Equal("example.jar", version.PrimaryFile?.FileName);
        Assert.Equal(CommunityResourceVersionChannel.Release, version.Channel);
        Assert.Equal(2, version.Dependencies.Count);
        Assert.Contains(version.Dependencies, item =>
            item.ProjectId == "dependency-a" && item.Type == CommunityResourceDependencyType.Required);
    }

    [Fact]
    public void ParseCatalog_RejectsUntrustedDownloadHost()
    {
        var result = ModrinthCommunityResourceVersionParser.ParseCatalog(
            """
            [{"id":"v","project_id":"p","name":"Unsafe","version_number":"1",
              "files":[{"filename":"unsafe.jar","primary":true,"size":12,
                "url":"https://example.invalid/unsafe.jar",
                "hashes":{"sha1":"0123456789abcdef0123456789abcdef01234567"}}]}]
            """);

        Assert.Empty(result.Versions);
        Assert.Contains(result.Errors, error => error.Contains("没有可校验", StringComparison.Ordinal));
    }
}
