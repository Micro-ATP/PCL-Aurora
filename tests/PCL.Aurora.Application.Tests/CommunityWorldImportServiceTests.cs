using System.IO.Compression;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class CommunityWorldImportServiceTests
{
    [Fact]
    public async Task ImportAsync_StripsSingleWorldRootAndPublishesAtomically()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var service = new CommunityWorldImportService(new ArchiveDownloadExecutor(
                ("__MACOSX/metadata", new byte[] { 0 }),
                ("Example World/level.dat", new byte[] { 1, 2, 3 }),
                ("Example World/region/r.0.0.mca", new byte[] { 4, 5 })));

            var result = await service.ImportAsync(CreateProject(), CreateVersion(), root, "My World");

            Assert.Equal(Path.Combine(root, "My World"), result.WorldDirectory);
            Assert.True(File.Exists(Path.Combine(result.WorldDirectory, "level.dat")));
            Assert.True(File.Exists(Path.Combine(result.WorldDirectory, "region", "r.0.0.mca")));
            Assert.False(Directory.Exists(Path.Combine(result.WorldDirectory, "__MACOSX")));
            Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(root),
                path => path.EndsWith(".partial", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_RejectsTraversalAndCleansStagingDirectory()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var service = new CommunityWorldImportService(new ArchiveDownloadExecutor(
                ("level.dat", new byte[] { 1 }),
                ("../outside.txt", new byte[] { 2 })));

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.ImportAsync(CreateProject(), CreateVersion(), root, "Unsafe World"));

            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(root)!, "outside.txt")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CommunityResourceProject CreateProject() =>
        new(
            "307740", "oneblock", "Oneblock", "A world", "Author", CommunityResourceType.World,
            new Uri("https://www.curseforge.com/minecraft/worlds/oneblock"), null,
            1, 0, null, null, ["survival"], ["1.21.1"]);

    private static CommunityResourceVersion CreateVersion() =>
        new(
            "8340194", "307740", "Oneblock", "4.3.7", CommunityResourceVersionChannel.Release,
            null, 1, ["1.21.1"], [],
            [new("oneblock.zip", new Uri("https://edge.forgecdn.net/files/8340/194/oneblock.zip"),
                new string('a', 40), 123, true)],
            []);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aurora-world-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class ArchiveDownloadExecutor(params (string Path, byte[] Content)[] entries)
        : IMinecraftDownloadExecutor
    {
        public Task ExecuteAsync(
            MinecraftDownloadPlan downloadPlan,
            string minecraftRootDirectory,
            CancellationToken cancellationToken = default)
        {
            var destination = Path.Combine(minecraftRootDirectory, downloadPlan.Artifacts.Single().RelativePath);
            using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
            foreach (var (path, content) in entries)
            {
                var entry = archive.CreateEntry(path);
                using var stream = entry.Open();
                stream.Write(content);
            }

            return Task.CompletedTask;
        }

        public Task ExecuteAsync(
            MinecraftAssetDownloadPlan downloadPlan,
            string minecraftRootDirectory,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
