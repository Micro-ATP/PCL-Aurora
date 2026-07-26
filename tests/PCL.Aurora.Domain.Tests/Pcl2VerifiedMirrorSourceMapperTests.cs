namespace PCL.Aurora.Domain.Tests;

public sealed class Pcl2VerifiedMirrorSourceMapperTests
{
    private const string Sha1 = "0123456789abcdef0123456789abcdef01234567";

    [Theory]
    [InlineData("https://maven.fabricmc.net/net/fabricmc/example/1.0/example-1.0.jar", "https://bmclapi2.bangbang93.com/maven/net/fabricmc/example/1.0/example-1.0.jar")]
    [InlineData("https://maven.neoforged.net/releases/net/neoforged/neoforge/21.1.100/neoforge-21.1.100.jar", "https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/21.1.100/neoforge-21.1.100.jar")]
    [InlineData("https://libraries.minecraft.net/org/example/1.0/example-1.0.jar", "https://bmclapi2.bangbang93.com/maven/org/example/1.0/example-1.0.jar")]
    [InlineData("https://resources.download.minecraft.net/01/0123456789abcdef0123456789abcdef01234567", "https://bmclapi2.bangbang93.com/assets/01/0123456789abcdef0123456789abcdef01234567")]
    public void PreferMirrorWhenVerified_MapsPcl2OfficialSourcesAndKeepsFallback(string official, string mirror)
    {
        var artifact = new MinecraftDownloadArtifact("测试", "libraries/test.jar", new Uri(official), Sha1, 1);

        var mapped = Pcl2VerifiedMirrorSourceMapper.PreferMirrorWhenVerified(artifact);

        Assert.Equal(mirror, mapped.Url.AbsoluteUri);
        Assert.Equal(official, Assert.Single(mapped.AlternativeUrls!).AbsoluteUri);
    }

    [Fact]
    public void PreferMirrorWhenVerified_LeavesUnknownOrUnverifiedSourceUnchanged()
    {
        var unknown = new MinecraftDownloadArtifact("未知", "x", new Uri("https://example.invalid/file.jar"), Sha1, 1);
        var unverified = new MinecraftDownloadArtifact("未校验", "x", new Uri("https://libraries.minecraft.net/x.jar"), null, 1);

        Assert.Same(unknown, Pcl2VerifiedMirrorSourceMapper.PreferMirrorWhenVerified(unknown));
        Assert.Same(unverified, Pcl2VerifiedMirrorSourceMapper.PreferMirrorWhenVerified(unverified));
    }
}
