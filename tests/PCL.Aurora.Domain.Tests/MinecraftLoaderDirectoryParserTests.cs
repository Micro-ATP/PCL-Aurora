using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftLoaderDirectoryParserTests
{
    [Fact]
    public void ParseDirectories_ReproducesPclCeGroupingAndPackageMetadata()
    {
        var forgeGroups = MinecraftLoaderDirectoryParser.ParseForgeMinecraftVersions("""["1.20.1", "1.19.4"]""");
        var forge = MinecraftLoaderDirectoryParser.ParseForgeVersions(
            "1.20.1",
            """[{"version":"47.2.0","modified":"2024-01-01","recommended":true,"files":[{"category":"installer","format":"jar","hash":"md5"}]}]""");
        var neoForge = MinecraftLoaderDirectoryParser.ParseNeoForgeVersions(
            """{"files":[{"name":"20.1.2-beta","type":"DIRECTORY"},{"name":"20.1.1","type":"DIRECTORY"}]}""");
        var optiFine = MinecraftLoaderDirectoryParser.ParseOptiFineVersions(
            """[{"mcversion":"1.20.1","type":"HD_U","patch":"I6","filename":"OptiFine_1.20.1_HD_U_I6.jar","forge":"Forge 47.2.0"}]""");
        var fabric = MinecraftLoaderDirectoryParser.ParseFabricInstallers(
            """[{"version":"1.0.3","stable":true,"url":"https://maven.fabricmc.net/net/fabricmc/fabric-installer/1.0.3/fabric-installer-1.0.3.jar"}]""");

        Assert.Equal(["1.20.1", "1.19.4"], forgeGroups.Groups.Select(group => group.Key));
        Assert.True(forgeGroups.Groups.All(group => group.IsLazy));
        var forgePackage = Assert.Single(forge.Groups[0].Entries);
        Assert.Equal("Forge-1.20.1-47.2.0.jar", forgePackage.FileName);
        Assert.Equal("bmclapi2.bangbang93.com", forgePackage.DownloadUri.Host);
        Assert.Single(forgePackage.AlternativeUris);

        Assert.Equal("1.20.1 (2)", neoForge.Groups[0].Title);
        Assert.Equal("20.1.2-beta", neoForge.Groups[0].Entries[0].Version);
        Assert.Equal("1.20 (1)", optiFine.Groups[0].Title);
        Assert.Contains("HD_U/I6", optiFine.Groups[0].Entries[0].DownloadUri.AbsolutePath, StringComparison.Ordinal);
        Assert.False(fabric.Groups[0].IsCollapsible);
        Assert.Equal("版本列表 (1)", fabric.Groups[0].Title);
    }

    [Fact]
    public void ParseAdditionalDirectories_PreservesTheirDistinctPclCeLayouts()
    {
        var cleanroom = MinecraftLoaderDirectoryParser.ParseCleanroomVersions(
            """[{"tag_name":"0.6.8-alpha","html_url":"https://github.com/CleanroomMC/Cleanroom/releases/tag/0.6.8-alpha","assets":[{"name":"cleanroom-0.6.8-alpha-installer.jar","browser_download_url":"https://github.com/CleanroomMC/Cleanroom/releases/download/0.6.8-alpha/cleanroom-0.6.8-alpha-installer.jar"}]}]""");
        var legacyFabric = MinecraftLoaderDirectoryParser.ParseLegacyFabricInstallers(
            """{"installer":[{"url":"https://maven.legacyfabric.net/net/legacyfabric/fabric-installer/1.1.1/fabric-installer-1.1.1.jar","version":"1.1.1","stable":true}]}""");
        var labyMod = MinecraftLoaderDirectoryParser.ParseLabyModVersions(
            """{"labyModVersion":"4.6.12","releaseTime":"2026-07-27T12:00:00Z"}""",
            """{"labyModVersion":"4.6.13-beta","releaseTime":"2026-07-28T12:00:00Z"}""");
        var liteLoader = MinecraftLoaderDirectoryParser.ParseLiteLoaderVersions(
            """{"versions":{"1.12.2":{"artefacts":{"com.mumfrey:liteloader":{"latest":{"stream":"RELEASE","version":"1.12.2-SNAPSHOT","timestamp":"1704067200000"}}}},"1.7.10":{"snapshots":{"com.mumfrey:liteloader":{"latest":{"stream":"SNAPSHOT","version":"1.7.10-SNAPSHOT","timestamp":1704067200000}}}}}}""");

        Assert.Equal("1.12.2 (1)", Assert.Single(cleanroom.Groups).Title);
        Assert.Equal("github.com", Assert.Single(cleanroom.Groups[0].Entries).DownloadUri.Host);
        Assert.False(Assert.Single(legacyFabric.Groups).IsCollapsible);
        Assert.True(Assert.Single(legacyFabric.Groups[0].Entries).IsRecommended);
        Assert.Equal(["4.6.12 正式版", "4.6.13-beta 快照版"], labyMod.Groups[0].Entries.Select(entry => entry.DisplayName));
        Assert.Equal(["1.12", "1.7"], liteLoader.Groups.Select(group => group.Key));
        Assert.All(liteLoader.Groups.SelectMany(group => group.Entries), entry => Assert.Equal(Uri.UriSchemeHttps, entry.DownloadUri.Scheme));
    }
}
