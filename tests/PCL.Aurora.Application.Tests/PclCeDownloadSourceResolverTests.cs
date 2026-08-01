namespace PCL.Aurora.Application.Tests;

public sealed class PclCeDownloadSourceResolverTests
{
    [Theory]
    [InlineData("https://maven.minecraftforge.net/net/minecraftforge/forge/1.20.1/forge.jar", "https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/1.20.1/forge.jar")]
    [InlineData("https://meta.fabricmc.net/v2/versions/installer", "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/installer")]
    [InlineData("https://maven.neoforged.net/releases/net/neoforged/neoforge/20.1/neoforge.jar", "https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/20.1/neoforge.jar")]
    public void ToBmclapi_MapsLoaderMetadataAndArtifacts(string official, string expected)
    {
        var mirror = PclCeDownloadSourceResolver.ToBmclapi(new Uri(official));

        Assert.Equal(expected, mirror?.AbsoluteUri);
        Assert.True(PclCeDownloadSourceResolver.IsMirror(mirror!));
        Assert.False(PclCeDownloadSourceResolver.IsMirror(new Uri(official)));
    }

    private static readonly Uri Official = new("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");

    [Fact]
    public void Order_MirrorPreferencePlacesMirrorFirstAndKeepsOfficialFallback()
    {
        var mirror = PclCeDownloadSourceResolver.ToBmclapi(Official);

        var result = PclCeDownloadSourceResolver.Order(DownloadSourcePreference.Mirror, Official, mirror);

        Assert.Equal("bmclapi2.bangbang93.com", result[0].Host);
        Assert.Equal(Official, result[1]);
    }

    [Fact]
    public void Order_OfficialPreferencePlacesOfficialFirstAndKeepsMirrorFallback()
    {
        var mirror = PclCeDownloadSourceResolver.ToBmclapi(Official);

        var result = PclCeDownloadSourceResolver.Order(DownloadSourcePreference.Official, Official, mirror);

        Assert.Equal(Official, result[0]);
        Assert.Equal("bmclapi2.bangbang93.com", result[1].Host);
    }

    [Fact]
    public void ToCommunityMirror_RewritesApiAndDownloadHosts()
    {
        var api = PclCeDownloadSourceResolver.ToCommunityMirror(new Uri("https://api.modrinth.com/v2/search"));
        var file = PclCeDownloadSourceResolver.ToCommunityMirror(
            new Uri("https://cdn.modrinth.com/data/id/versions/version/file.jar"));

        Assert.Equal("https://mod.mcimirror.top/modrinth/v2/search", api!.AbsoluteUri);
        Assert.Equal("https://mod.mcimirror.top/data/id/versions/version/file.jar", file!.AbsoluteUri);
    }

    [Fact]
    public void OrderCommunity_OfficialPreferenceDoesNotUseMirror()
    {
        var mirror = PclCeDownloadSourceResolver.ToCommunityMirror(
            new Uri("https://api.modrinth.com/v2/search"));

        var result = PclCeDownloadSourceResolver.OrderCommunity(
            DownloadSourcePreference.Official,
            new Uri("https://api.modrinth.com/v2/search"),
            mirror);

        Assert.Equal("api.modrinth.com", Assert.Single(result).Host);
    }
}
