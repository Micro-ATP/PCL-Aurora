using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftAssetIndexTests
{
    [Fact]
    public void ParseAndPlan_CreatesHashAddressedObjectDownloads()
    {
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        var inspection = MinecraftAssetIndexParser.Parse(
            "17",
            $$"""
              { "objects": { "minecraft/sounds/test.ogg": { "hash": "{{hash}}", "size": 42 } } }
              """);

        var plan = MinecraftAssetDownloadPlanBuilder.Create(inspection);

        Assert.True(inspection.IsSuccess);
        Assert.True(plan.IsReady);
        var artifact = Assert.Single(plan.Artifacts);
        Assert.Equal($"assets/objects/01/{hash}", artifact.RelativePath);
        Assert.Equal($"https://bmclapi2.bangbang93.com/assets/01/{hash}", artifact.Url.ToString());
        Assert.Equal($"https://resources.download.minecraft.net/01/{hash}", Assert.Single(artifact.AlternativeUrls!).ToString());
        Assert.Equal(hash, artifact.Sha1);
    }

    [Fact]
    public void Parse_RejectsUnsafeNamesAndInvalidHashes()
    {
        var inspection = MinecraftAssetIndexParser.Parse(
            "17",
            """
            { "objects": { "../escape": { "hash": "not-a-sha1", "size": 1 } } }
            """);

        Assert.False(inspection.IsSuccess);
        Assert.Null(inspection.Index);
        Assert.NotEmpty(inspection.Errors);
    }
}
