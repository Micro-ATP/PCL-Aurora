using PCL.Aurora.Domain;
using PCL.Aurora.Infrastructure;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class JsonCommunityFavoritesStoreTests : IDisposable
{
    private readonly string applicationDataDirectory = Path.Combine(
        Path.GetTempPath(),
        $"pcl-aurora-favorites-{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadAsync_ReturnsDefaultFolderWhenFileIsMissing()
    {
        var result = await CreateStore().LoadAsync();

        var folder = Assert.Single(result.Folders);
        Assert.Equal("默认", folder.Name);
        Assert.Empty(folder.Projects);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsProjectSnapshots()
    {
        var store = CreateStore();
        var folder = CommunityFavoriteFolder.Create("常用", [CreateProject()]);

        await store.SaveAsync([folder]);
        var result = await store.LoadAsync();

        var loaded = Assert.Single(result.Folders);
        Assert.Equal("常用", loaded.Name);
        Assert.Equal("sodium", Assert.Single(loaded.Projects).Slug);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task LoadAsync_RecoversFromInvalidJsonWithoutOverwritingIt()
    {
        Directory.CreateDirectory(applicationDataDirectory);
        var path = Path.Combine(applicationDataDirectory, "community-favorites.json");
        await File.WriteAllTextAsync(path, "{not-json");

        var result = await CreateStore().LoadAsync();

        Assert.Single(result.Folders);
        Assert.NotNull(result.Warning);
        Assert.Equal("{not-json", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task LoadAsync_RecoversWhenFolderProjectListIsNull()
    {
        Directory.CreateDirectory(applicationDataDirectory);
        var path = Path.Combine(applicationDataDirectory, "community-favorites.json");
        await File.WriteAllTextAsync(
            path,
            "[{\"id\":\"d698834f-cf57-4f51-bf93-92c20b77200c\",\"name\":\"损坏收藏夹\",\"projects\":null}]");

        var result = await CreateStore().LoadAsync();

        Assert.Equal("默认", Assert.Single(result.Folders).Name);
        Assert.NotNull(result.Warning);
    }

    public void Dispose()
    {
        if (Directory.Exists(applicationDataDirectory))
        {
            Directory.Delete(applicationDataDirectory, recursive: true);
        }
    }

    private JsonCommunityFavoritesStore CreateStore() =>
        new(new FixedPlatformPaths(applicationDataDirectory));

    private static CommunityResourceProject CreateProject() =>
        new(
            "AANobbMI", "sodium", "Sodium", "Rendering engine", "jellysquid3",
            CommunityResourceType.Mod, new Uri("https://modrinth.com/mod/sodium"),
            new Uri("https://cdn.modrinth.com/data/AANobbMI/icon.png"), 10, 2,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "mc1.21.1-0.6.0", ["optimization"], ["1.21.1"])
        {
            Loaders = ["fabric"],
            TranslatedTitle = "钠 (Sodium)",
        };

    private sealed class FixedPlatformPaths(string applicationDataDirectory) : IPlatformPaths
    {
        public PlatformPaths Get() => new(applicationDataDirectory, Path.Combine(applicationDataDirectory, "cache"));
    }
}
