using System.Net;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftVersionProvisioningServiceTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-provision-{Guid.NewGuid():N}");

    [Fact]
    public async Task ProvisionAsync_WritesOnlyValidatedMatchingMetadata()
    {
        using var client = new HttpClient(new StaticResponseHandler("""{ "id": "1.21.4", "type": "release" }"""));
        var service = new MinecraftVersionProvisioningService(client, new FixedRootDirectoryProvider(rootDirectory));
        var version = new MinecraftVersionCatalogEntry("1.21.4", "release", new Uri("https://example.invalid/1.21.4.json"), DateTimeOffset.UtcNow);

        var instance = await service.ProvisionAsync(version);

        Assert.Equal("1.21.4", instance.VersionId);
        Assert.True(File.Exists(Path.Combine(rootDirectory, "versions", "1.21.4", "1.21.4.json")));
    }

    [Fact]
    public async Task ProvisionAsync_RejectsMismatchedMetadataWithoutCreatingInstanceDirectory()
    {
        using var client = new HttpClient(new StaticResponseHandler("""{ "id": "other", "type": "release" }"""));
        var service = new MinecraftVersionProvisioningService(client, new FixedRootDirectoryProvider(rootDirectory));
        var version = new MinecraftVersionCatalogEntry("1.21.4", "release", new Uri("https://example.invalid/1.21.4.json"), DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ProvisionAsync(version));

        Assert.False(Directory.Exists(Path.Combine(rootDirectory, "versions", "1.21.4")));
    }

    [Fact]
    public async Task ProvisionAsync_UsesExplicitMinecraftRootDirectory()
    {
        var defaultRoot = Path.Combine(rootDirectory, "default");
        var selectedRoot = Path.Combine(rootDirectory, "selected");
        using var client = new HttpClient(new StaticResponseHandler("""{ "id": "1.21.4", "type": "release" }"""));
        var service = new MinecraftVersionProvisioningService(client, new FixedRootDirectoryProvider(defaultRoot));
        var version = new MinecraftVersionCatalogEntry("1.21.4", "release", new Uri("https://example.invalid/1.21.4.json"), DateTimeOffset.UtcNow);

        var instance = await service.ProvisionAsync(version, selectedRoot);

        Assert.Equal(Path.Combine(selectedRoot, "versions", "1.21.4"), instance.DirectoryPath);
        Assert.True(File.Exists(Path.Combine(selectedRoot, "versions", "1.21.4", "1.21.4.json")));
        Assert.False(Directory.Exists(defaultRoot));
    }

    [Fact]
    public async Task ProvisionAsync_ReusesValidatedExistingInstanceForResume()
    {
        var instanceDirectory = Path.Combine(rootDirectory, "versions", "1.21.4");
        Directory.CreateDirectory(instanceDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(instanceDirectory, "1.21.4.json"),
            """{ "id": "1.21.4", "type": "release" }""");
        var handler = new StaticResponseHandler("""{ "id": "unexpected" }""");
        using var client = new HttpClient(handler);
        var service = new MinecraftVersionProvisioningService(client, new FixedRootDirectoryProvider(rootDirectory));
        var version = new MinecraftVersionCatalogEntry("1.21.4", "release", new Uri("https://example.invalid/1.21.4.json"), DateTimeOffset.UtcNow);

        var instance = await service.ProvisionAsync(version);

        Assert.Equal(instanceDirectory, instance.DirectoryPath);
        Assert.Equal(0, handler.RequestCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private sealed class FixedRootDirectoryProvider(string rootDirectory) : IMinecraftRootDirectoryProvider
    {
        public string GetRootDirectory() => rootDirectory;
    }

    private sealed class StaticResponseHandler(string content) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(CreateResponse());

        private HttpResponseMessage CreateResponse()
        {
            RequestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
        }
    }
}
