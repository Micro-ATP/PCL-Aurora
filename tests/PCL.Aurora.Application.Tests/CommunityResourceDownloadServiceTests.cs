using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class CommunityResourceDownloadServiceTests
{
    [Fact]
    public async Task DownloadAsync_BuildsVerifiedSingleFilePlanForChosenDirectory()
    {
        var executor = new CapturingDownloadExecutor();
        var service = new CommunityResourceDownloadService(executor);
        var project = CreateProject();
        var version = CreateVersion("sodium.jar");
        var destination = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "aurora-chosen-folder"));

        var result = await service.DownloadAsync(project, version, destination);

        Assert.Equal(Path.Combine(destination, "sodium.jar"), result);
        Assert.Equal(destination, executor.RootDirectory);
        var artifact = Assert.Single(executor.Plan!.Artifacts);
        Assert.Equal("sodium.jar", artifact.RelativePath);
        Assert.Equal(version.PrimaryFile!.Sha1, artifact.Sha1);
        Assert.Equal(version.PrimaryFile.Size, artifact.Size);
    }

    [Fact]
    public async Task DownloadAsync_RejectsFileNameContainingDirectories()
    {
        var service = new CommunityResourceDownloadService(new CapturingDownloadExecutor());

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadAsync(CreateProject(), CreateVersion("../outside.jar"), Path.GetTempPath()));

        Assert.Contains("不安全", exception.Message, StringComparison.Ordinal);
    }

    private static CommunityResourceProject CreateProject() =>
        new(
            "AANobbMI", "sodium", "Sodium", "Rendering engine", "jellysquid3",
            CommunityResourceType.Mod, new Uri("https://modrinth.com/mod/sodium"), null,
            10, 2, null, null, [], ["1.21.1"]);

    private static CommunityResourceVersion CreateVersion(string fileName) =>
        new(
            "version", "AANobbMI", "Sodium 0.6", "0.6.0", CommunityResourceVersionChannel.Release,
            DateTimeOffset.UtcNow, 5, ["1.21.1"], ["fabric"],
            [new(fileName, new Uri("https://cdn.modrinth.com/data/AANobbMI/version/sodium.jar"), new string('a', 40), 123, true)],
            []);

    private sealed class CapturingDownloadExecutor : IMinecraftDownloadExecutor
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
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
