using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftLoaderPackageDownloadServiceTests
{
    [Fact]
    public async Task DownloadAsync_UsesExplicitFileNameAndPreservesMirrorAndMinimumSize()
    {
        var executor = new RecordingExecutor();
        var service = new MinecraftLoaderPackageDownloadService(executor);
        var root = Path.Combine(Path.GetTempPath(), "pcl-aurora-loader-package-tests");
        var destination = Path.Combine(root, "custom-installer.jar");
        var package = new MinecraftLoaderPackageEntry(
            MinecraftLoaderKind.NeoForge,
            "1.20.1",
            "20.1.1",
            "20.1.1",
            MinecraftLoaderChannel.Release,
            false,
            "NeoForge-1.20.1-20.1.1.jar",
            new Uri("https://mirror.example/installer.jar"),
            [new Uri("https://official.example/installer.jar")],
            null,
            "稳定版");

        var result = await service.DownloadAsync(package, destination);

        Assert.Equal(Path.GetFullPath(destination), result);
        Assert.Equal(Path.GetFullPath(root), executor.RootDirectory);
        var artifact = Assert.Single(executor.Plan!.Artifacts);
        Assert.Equal("custom-installer.jar", artifact.RelativePath);
        Assert.Equal(65_536, artifact.MinimumSize);
        Assert.Equal("official.example", Assert.Single(artifact.AlternativeUrls!).Host);
    }

    private sealed class RecordingExecutor : IMinecraftDownloadExecutor
    {
        public MinecraftDownloadPlan? Plan { get; private set; }

        public string? RootDirectory { get; private set; }

        public Task ExecuteAsync(
            MinecraftDownloadPlan downloadPlan,
            string minecraftRootDirectory,
            CancellationToken cancellationToken = default)
        {
            Plan = downloadPlan;
            RootDirectory = minecraftRootDirectory;
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(
            MinecraftAssetDownloadPlan downloadPlan,
            string minecraftRootDirectory,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
