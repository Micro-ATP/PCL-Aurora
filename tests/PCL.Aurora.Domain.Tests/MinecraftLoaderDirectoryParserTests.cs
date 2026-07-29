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
}
