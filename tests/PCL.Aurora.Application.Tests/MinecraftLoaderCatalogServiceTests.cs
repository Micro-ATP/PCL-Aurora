using PCL.Aurora.Application;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftLoaderCatalogServiceTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-loaders-{Guid.NewGuid():N}");

    [Fact]
    public async Task ReadAsync_ReadsUserSpecifiedLocalCatalogWithoutNetworkOrWrites()
    {
        Directory.CreateDirectory(temporaryDirectory);
        var catalogPath = Path.Combine(temporaryDirectory, "loaders.json");
        await File.WriteAllTextAsync(
            catalogPath,
            """
            {
              "source": "本地测试目录",
              "loaders": [
                { "kind": "fabric", "minecraftVersion": "1.21.4", "version": "0.16.10" }
              ]
            }
            """);

        var result = await new MinecraftLoaderCatalogService().ReadAsync(catalogPath);

        Assert.True(result.IsSuccess);
        Assert.Equal("本地测试目录", result.Catalog!.SourceName);
        Assert.Single(result.Catalog.Entries);
        Assert.Single(Directory.EnumerateFiles(temporaryDirectory));
    }

    [Fact]
    public async Task ReadAsync_ReportsMissingFileWithoutCreatingIt()
    {
        var catalogPath = Path.Combine(temporaryDirectory, "missing.json");

        var result = await new MinecraftLoaderCatalogService().ReadAsync(catalogPath);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("未找到", StringComparison.Ordinal));
        Assert.False(File.Exists(catalogPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
