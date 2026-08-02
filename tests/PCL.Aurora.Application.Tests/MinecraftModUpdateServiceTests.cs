using System.Net;
using System.Text;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftModUpdateServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"pcl-aurora-mod-update-{Guid.NewGuid():N}");

    [Fact]
    public async Task CheckAndApply_IdentifiesCompatibleUpdateAndReplacesLocalFile()
    {
        var instanceDirectory = Path.Combine(root, "versions", "Fabric 1.20.1");
        var modDirectory = Path.Combine(instanceDirectory, "mods");
        Directory.CreateDirectory(modDirectory);
        var localPath = Path.Combine(modDirectory, "example-old.jar");
        await File.WriteAllTextAsync(localPath, "old-mod");
        var instance = new MinecraftInstance(
            "Fabric 1.20.1",
            instanceDirectory,
            "fabric-loader",
            "release",
            null,
            MinecraftInstanceStatus.Valid,
            BaseVersionId: "1.20.1",
            InstalledLoader: new(MinecraftLoaderKind.Fabric, "0.16.0", "1.20.1"));
        var current = CreateVersion("current", "1.0.0", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), "example-old.jar");
        var latest = CreateVersion("latest", "2.0.0", new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), "example-new.jar");
        using var httpClient = new HttpClient(new VersionHandler(CreateVersionJson(current)));
        var executor = new WritingDownloadExecutor("new-mod");
        var service = new MinecraftModUpdateService(
            httpClient,
            new MinecraftInstanceManagementService(),
            new FixedVersionService(latest),
            executor);

        var check = await service.CheckAsync(instance, MinecraftInstanceIsolationMode.All);

        var update = Assert.Single(check.Updates);
        Assert.Equal("1.0.0 → 2.0.0", update.VersionSummary);
        var applied = await service.ApplyAsync(instance, MinecraftInstanceIsolationMode.All, check.Updates);
        Assert.Equal(1, applied.UpdatedCount);
        Assert.False(File.Exists(localPath));
        Assert.Equal("new-mod", await File.ReadAllTextAsync(Path.Combine(modDirectory, "example-new.jar")));
    }

    private static CommunityResourceVersion CreateVersion(
        string id,
        string version,
        DateTimeOffset published,
        string fileName) =>
        new(
            id,
            "project",
            "Example Mod",
            version,
            CommunityResourceVersionChannel.Release,
            published,
            1,
            ["1.20.1"],
            ["fabric"],
            [new(fileName, new Uri("https://cdn.modrinth.com/data/project/versions/file.jar"), new string('a', 40), 7, true)],
            []);

    private static string CreateVersionJson(CommunityResourceVersion version) => $$"""
        {
          "id": "{{version.Id}}",
          "project_id": "{{version.ProjectId}}",
          "name": "{{version.Name}}",
          "version_number": "{{version.VersionNumber}}",
          "version_type": "release",
          "date_published": "{{version.PublishedAt:O}}",
          "downloads": 1,
          "game_versions": ["1.20.1"],
          "loaders": ["fabric"],
          "files": [{
            "filename": "{{version.PrimaryFile!.FileName}}",
            "url": "https://cdn.modrinth.com/data/project/versions/file.jar",
            "size": 7,
            "primary": true,
            "hashes": { "sha1": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }
          }],
          "dependencies": []
        }
        """;

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class VersionHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class FixedVersionService(CommunityResourceVersion latest) : ICommunityResourceVersionService
    {
        public Task<CommunityResourceVersionCatalog> GetProjectVersionsAsync(
            string projectId,
            string? gameVersion,
            CommunityResourceLoader loader,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommunityResourceVersionCatalog([latest], []));

        public Task<CommunityResourceVersionCatalog> GetVersionAsync(
            string versionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class WritingDownloadExecutor(string content) : IMinecraftDownloadExecutor
    {
        public async Task ExecuteAsync(
            MinecraftDownloadPlan downloadPlan,
            string minecraftRootDirectory,
            CancellationToken cancellationToken = default)
        {
            var artifact = Assert.Single(downloadPlan.Artifacts);
            await File.WriteAllTextAsync(
                Path.Combine(minecraftRootDirectory, artifact.RelativePath),
                content,
                cancellationToken);
        }

        public Task ExecuteAsync(
            MinecraftAssetDownloadPlan downloadPlan,
            string minecraftRootDirectory,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
