using PCL.Aurora.Domain;
using PCL.Aurora.Infrastructure;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftAssetMapperTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-asset-copy-{Guid.NewGuid():N}");

    [Fact]
    public async Task PrepareAsync_VerifiesAndCopiesVirtualAsset()
    {
        const string hash = "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d";
        var sourcePath = Path.Combine(rootDirectory, "assets", "objects", "aa", hash);
        var targetDirectory = Path.Combine(rootDirectory, "assets", "virtual", "legacy");
        var destinationPath = Path.Combine(targetDirectory, "minecraft", "test.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "hello");
        var plan = new MinecraftAssetMappingPlan(
            targetDirectory,
            [new(new("minecraft/test.txt", hash, 5), sourcePath, destinationPath)],
            [],
            []);

        var result = await new MinecraftAssetMapper().PrepareAsync(plan);

        Assert.True(result.IsReady);
        Assert.Equal(1, result.MappedFileCount);
        Assert.Equal("hello", await File.ReadAllTextAsync(destinationPath));
    }

    [Fact]
    public async Task PrepareAsync_RejectsHashMismatchWithoutWritingDestination()
    {
        const string hash = "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d";
        var sourcePath = Path.Combine(rootDirectory, "assets", "objects", "aa", hash);
        var targetDirectory = Path.Combine(rootDirectory, "assets", "virtual", "legacy");
        var destinationPath = Path.Combine(targetDirectory, "minecraft", "test.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "wrong");
        var plan = new MinecraftAssetMappingPlan(
            targetDirectory,
            [new(new("minecraft/test.txt", hash, 5), sourcePath, destinationPath)],
            [],
            []);

        var result = await new MinecraftAssetMapper().PrepareAsync(plan);

        Assert.False(result.IsReady);
        Assert.Contains(result.BlockingReasons, reason => reason.Contains("SHA-1", StringComparison.Ordinal));
        Assert.False(File.Exists(destinationPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
