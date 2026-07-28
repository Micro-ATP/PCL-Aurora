using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftVersionArchiveServiceTests : IDisposable
{
    private static readonly Uri MetadataUri = new("https://example.invalid/1.21.4.json");
    private static readonly Uri ClientUri = new("https://example.invalid/1.21.4.jar");
    private static readonly Uri ServerUri = new("https://example.invalid/server.jar");
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-archive-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveClientCoreAsync_PublishesValidatedVersionDirectory()
    {
        Directory.CreateDirectory(rootDirectory);
        var clientBytes = Encoding.UTF8.GetBytes("validated client");
        using var client = CreateClient(clientBytes, Encoding.UTF8.GetBytes("server"));
        var service = new MinecraftVersionArchiveService(client);

        var target = await service.SaveClientCoreAsync(CreateVersion(), rootDirectory);

        Assert.Equal(Path.Combine(rootDirectory, "1.21.4"), target);
        Assert.Equal(clientBytes, await File.ReadAllBytesAsync(Path.Combine(target, "1.21.4.jar")));
        Assert.Contains("\"id\":\"1.21.4\"", await File.ReadAllTextAsync(Path.Combine(target, "1.21.4.json")));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(rootDirectory), path => path.EndsWith(".partial", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveServerAsync_ChecksumFailurePreservesExistingDestination()
    {
        Directory.CreateDirectory(rootDirectory);
        var destination = Path.Combine(rootDirectory, "server.jar");
        var existingBytes = Encoding.UTF8.GetBytes("existing server");
        await File.WriteAllBytesAsync(destination, existingBytes);
        using var client = CreateClient(Encoding.UTF8.GetBytes("client"), Encoding.UTF8.GetBytes("invalid server"), invalidServerChecksum: true);
        var service = new MinecraftVersionArchiveService(client);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.SaveServerAsync(CreateVersion(), destination));

        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(destination));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(rootDirectory), path => path.EndsWith(".partial", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private static MinecraftVersionCatalogEntry CreateVersion() =>
        new("1.21.4", "release", MetadataUri, DateTimeOffset.UtcNow);

    private static HttpClient CreateClient(byte[] clientBytes, byte[] serverBytes, bool invalidServerChecksum = false)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            id = "1.21.4",
            downloads = new
            {
                client = new { url = ClientUri, sha1 = Sha1(clientBytes), size = clientBytes.Length },
                server = new
                {
                    url = ServerUri,
                    sha1 = invalidServerChecksum ? new string('0', 40) : Sha1(serverBytes),
                    size = serverBytes.Length,
                },
            },
        });
        return new HttpClient(new StaticResponseHandler(new Dictionary<Uri, byte[]>
        {
            [MetadataUri] = Encoding.UTF8.GetBytes(metadata),
            [ClientUri] = clientBytes,
            [ServerUri] = serverBytes,
        }));
    }

    private static string Sha1(byte[] content) => Convert.ToHexString(SHA1.HashData(content));

    private sealed class StaticResponseHandler(IReadOnlyDictionary<Uri, byte[]> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null || !responses.TryGetValue(request.RequestUri, out var content))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content),
            });
        }
    }
}
