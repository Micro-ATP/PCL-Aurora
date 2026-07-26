using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftVersionMetadataTests
{
    [Fact]
    public void Parse_ReadsStandardVersionMetadataAndDownloadDescriptors()
    {
        var result = MinecraftVersionMetadataParser.Parse(
            """
            {
              "id": "1.21.4",
              "type": "release",
              "releaseTime": "2024-12-03T00:00:00Z",
              "downloads": { "client": { "url": "https://example.invalid/client.jar", "sha1": "client-sha", "size": 123 } },
              "assetIndex": { "id": "17", "url": "https://example.invalid/assets.json", "sha1": "assets-sha", "size": 456 }
            }
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.21.4", result.Metadata!.Id);
        Assert.Equal("release", result.Metadata.Type);
        Assert.Equal(123, result.Metadata.ClientDownload!.Size);
        Assert.Equal("17", result.Metadata.AssetIndex!.Id);
    }

    [Fact]
    public void Resolve_InheritsMissingDownloadDescriptorsFromParent()
    {
        var child = new MinecraftVersionMetadata("fabric-1.21.4", "1.21.4", null, null, null,
            new MinecraftVersionAssetIndex("17", new Uri("https://example.invalid/assets.json"), null, null));
        var parent = new MinecraftVersionMetadata("1.21.4", null, "release", null,
            new MinecraftVersionDownload(new Uri("https://example.invalid/client.jar"), null, null), null);

        var inspection = MinecraftVersionMetadataResolver.Resolve([child, parent]);
        var plan = MinecraftDownloadPlanBuilder.Create(inspection.EffectiveMetadata);

        Assert.True(inspection.IsSuccess);
        Assert.Equal("fabric-1.21.4", inspection.EffectiveMetadata!.Id);
        Assert.NotNull(inspection.EffectiveMetadata.ClientDownload);
        Assert.NotNull(inspection.EffectiveMetadata.AssetIndex);
        Assert.True(plan.IsReady);
        Assert.Equal(2, plan.Artifacts.Count);
    }

    [Fact]
    public void Parse_RejectsInvalidJson()
    {
        var result = MinecraftVersionMetadataParser.Parse("{ invalid }");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Metadata);
        Assert.NotEmpty(result.Errors);
    }
}
