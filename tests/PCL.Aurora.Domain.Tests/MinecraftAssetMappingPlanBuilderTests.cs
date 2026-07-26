using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftAssetMappingPlanBuilderTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-asset-map-{Guid.NewGuid():N}");

    [Fact]
    public async Task Build_MapsVirtualAssetsFromObjectsDirectory()
    {
        const string hash = "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d";
        var sourcePath = Path.Combine(rootDirectory, "assets", "objects", "aa", hash);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "hello");
        var instanceDirectory = Path.Combine(rootDirectory, "versions", "1.6.4");
        Directory.CreateDirectory(instanceDirectory);
        var inspection = MinecraftAssetIndexParser.Parse(
            "legacy",
            $$"""
            { "virtual": true, "objects": { "minecraft/lang/en_us.lang": { "hash": "{{hash}}", "size": 5 } } }
            """);

        var plan = MinecraftAssetMappingPlanBuilder.Build(inspection, rootDirectory, instanceDirectory);

        Assert.True(plan.IsReady);
        var entry = Assert.Single(plan.Entries);
        Assert.Equal(sourcePath, entry.SourcePath);
        Assert.Equal(Path.Combine(rootDirectory, "assets", "virtual", "legacy", "minecraft", "lang", "en_us.lang"), entry.DestinationPath);
    }

    [Fact]
    public void Build_ReportsMissingObjectFiles()
    {
        const string hash = "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d";
        var inspection = MinecraftAssetIndexParser.Parse(
            "legacy",
            $$"""{ "virtual": true, "objects": { "minecraft/test": { "hash": "{{hash}}", "size": 5 } } }""");

        var plan = MinecraftAssetMappingPlanBuilder.Build(
            inspection,
            rootDirectory,
            Path.Combine(rootDirectory, "versions", "1.6.4"));

        Assert.False(plan.IsReady);
        Assert.Single(plan.MissingFiles);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
