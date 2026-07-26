using PCL.Aurora.Domain;
using PCL.Aurora.Platform.MacOS;

namespace PCL.Aurora.Platform.Tests;

public sealed class MacOSMinecraftAssetIndexReaderTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-assets-{Guid.NewGuid():N}");

    [Fact]
    public async Task ReadAsync_ReadsOnlyTheExpectedLocalAssetIndex()
    {
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        var instanceDirectory = Path.Combine(rootDirectory, "versions", "1.21.4");
        var indexesDirectory = Path.Combine(rootDirectory, "assets", "indexes");
        Directory.CreateDirectory(instanceDirectory);
        Directory.CreateDirectory(indexesDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(indexesDirectory, "17.json"),
            $$"""{ "objects": { "minecraft/test": { "hash": "{{hash}}", "size": 1 } } }""");
        var instance = new MinecraftInstance("1.21.4", instanceDirectory, "1.21.4", null, null, MinecraftInstanceStatus.Valid);

        var result = await new MacOSMinecraftAssetIndexReader().ReadAsync(instance, "17");

        Assert.True(result.IsSuccess);
        Assert.Equal(hash, Assert.Single(result.Index!.Objects).Hash);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
