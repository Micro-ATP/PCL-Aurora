using System.Net;
using System.Security.Cryptography;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Infrastructure;

namespace PCL.Aurora.Application.Tests;

public sealed class CommunityResourceInstallationServiceTests : IDisposable
{
    private readonly string minecraftDirectory = Path.Combine(
        Path.GetTempPath(),
        $"pcl-aurora-community-{Guid.NewGuid():N}");

    private string InstanceDirectory => Path.Combine(minecraftDirectory, "versions", "Test");

    [Fact]
    public async Task InstallAsync_DownloadsModAndRequiredDependencyToModsDirectory()
    {
        var rootBytes = "root-mod"u8.ToArray();
        var dependencyBytes = "dependency-mod"u8.ToArray();
        var root = CreateVersion(
            "root-version",
            "root-project",
            "root.jar",
            rootBytes,
            [new("dependency-project", null, null, CommunityResourceDependencyType.Required)]);
        var dependency = CreateVersion(
            "dependency-version",
            "dependency-project",
            "dependency.jar",
            dependencyBytes,
            []);
        var handler = new FileHandler(new Dictionary<string, byte[]>
        {
            [root.PrimaryFile!.Url.AbsoluteUri] = rootBytes,
            [dependency.PrimaryFile!.Url.AbsoluteUri] = dependencyBytes,
        });
        using var client = new HttpClient(handler);
        var service = new CommunityResourceInstallationService(
            new StubVersionService(dependency),
            new MinecraftDownloadExecutor(client));
        var project = new CommunityResourceProject(
            "root-project", "root", "Root", "", "Author", CommunityResourceType.Mod,
            new Uri("https://modrinth.com/mod/root"), null, 0, 0, null, null, [], ["1.21.1"]);
        var instance = new MinecraftInstance(
            "Test", InstanceDirectory, "fabric-instance", "release", null, MinecraftInstanceStatus.Valid,
            BaseVersionId: "1.21.1",
            InstalledLoader: new(MinecraftLoaderKind.Fabric, "0.16", "1.21.1"));

        var result = await service.InstallAsync(project, root, instance);

        Assert.Equal(2, result.InstalledFileCount);
        Assert.Equal(1, result.InstalledDependencyCount);
        Assert.Equal(rootBytes, await File.ReadAllBytesAsync(Path.Combine(minecraftDirectory, "mods", "root.jar")));
        Assert.Equal(dependencyBytes, await File.ReadAllBytesAsync(Path.Combine(minecraftDirectory, "mods", "dependency.jar")));
    }

    [Fact]
    public async Task InstallAsync_DoesNotPutDataPackInTheInstanceRoot()
    {
        var bytes = "data-pack"u8.ToArray();
        var version = CreateVersion("v", "p", "data.zip", bytes, []);
        using var client = new HttpClient(new FileHandler(new Dictionary<string, byte[]>()));
        var service = new CommunityResourceInstallationService(
            new StubVersionService(version),
            new MinecraftDownloadExecutor(client));
        var project = new CommunityResourceProject(
            "p", "p", "Data", "", "Author", CommunityResourceType.DataPack,
            new Uri("https://modrinth.com/datapack/p"), null, 0, 0, null, null, [], ["1.21.1"]);
        var instance = new MinecraftInstance(
            "Test", InstanceDirectory, "1.21.1", "release", null, MinecraftInstanceStatus.Valid,
            BaseVersionId: "1.21.1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.InstallAsync(project, version, instance));

        Assert.Contains("选择存档世界", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(minecraftDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(minecraftDirectory))
        {
            Directory.Delete(minecraftDirectory, recursive: true);
        }
    }

    private static CommunityResourceVersion CreateVersion(
        string id,
        string projectId,
        string fileName,
        byte[] bytes,
        IReadOnlyList<CommunityResourceDependency> dependencies)
    {
        var sha1 = Convert.ToHexString(SHA1.HashData(bytes));
        var file = new CommunityResourceVersionFile(
            fileName,
            new Uri($"https://cdn.modrinth.com/data/{projectId}/versions/{id}/{fileName}"),
            sha1,
            bytes.Length,
            true);
        return new(
            id, projectId, fileName, "1.0.0", CommunityResourceVersionChannel.Release,
            DateTimeOffset.UtcNow, 0, ["1.21.1"], ["fabric"], [file], dependencies);
    }

    private sealed class StubVersionService(CommunityResourceVersion version) : ICommunityResourceVersionService
    {
        public Task<CommunityResourceVersionCatalog> GetProjectVersionsAsync(
            string projectId,
            string? gameVersion,
            CommunityResourceLoader loader,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommunityResourceVersionCatalog(
                string.Equals(projectId, version.ProjectId, StringComparison.Ordinal) ? [version] : [],
                []));

        public Task<CommunityResourceVersionCatalog> GetVersionAsync(
            string versionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommunityResourceVersionCatalog(
                string.Equals(versionId, version.Id, StringComparison.Ordinal) ? [version] : [],
                []));
    }

    private sealed class FileHandler(IReadOnlyDictionary<string, byte[]> files) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null && files.TryGetValue(request.RequestUri.AbsoluteUri, out var content))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(content),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
