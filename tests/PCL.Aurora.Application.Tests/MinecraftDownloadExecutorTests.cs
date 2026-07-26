using System.Net;
using System.Security.Cryptography;
using PCL.Aurora.Domain;
using PCL.Aurora.Infrastructure;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftDownloadExecutorTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-download-{Guid.NewGuid():N}");

    [Fact]
    public async Task ExecuteAsync_WritesVerifiedArtifactToItsPlannedPath()
    {
        var content = "PCL Aurora"u8.ToArray();
        using var client = new HttpClient(new StaticResponseHandler(content));
        var executor = new MinecraftDownloadExecutor(client);

        await executor.ExecuteAsync(CreatePlan(content), rootDirectory);

        var destinationPath = Path.Combine(rootDirectory, "versions", "1.21.4", "1.21.4.jar");
        Assert.Equal(content, await File.ReadAllBytesAsync(destinationPath));
    }

    [Fact]
    public async Task ExecuteAsync_OnHashMismatch_PreservesExistingFileAndCleansTemporaryFile()
    {
        var content = "new content"u8.ToArray();
        using var client = new HttpClient(new StaticResponseHandler(content));
        var executor = new MinecraftDownloadExecutor(client);
        var destinationPath = Path.Combine(rootDirectory, "versions", "1.21.4", "1.21.4.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await File.WriteAllTextAsync(destinationPath, "existing content");
        var invalidPlan = CreatePlan(content) with
        {
            Artifacts = [new MinecraftDownloadArtifact("Minecraft 客户端", "versions/1.21.4/1.21.4.jar", new Uri("https://example.invalid/client.jar"), "0000", content.Length)],
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => executor.ExecuteAsync(invalidPlan, rootDirectory));

        Assert.Equal("existing content", await File.ReadAllTextAsync(destinationPath));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(destinationPath)!, "*.partial"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_DoesNotCreateTheDestinationDirectory()
    {
        var content = "PCL Aurora"u8.ToArray();
        using var client = new HttpClient(new StaticResponseHandler(content));
        var executor = new MinecraftDownloadExecutor(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(CreatePlan(content), rootDirectory, cancellation.Token));

        Assert.False(Directory.Exists(rootDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private static MinecraftDownloadPlan CreatePlan(byte[] content) =>
        new(
            "1.21.4",
            [new MinecraftDownloadArtifact(
                "Minecraft 客户端",
                "versions/1.21.4/1.21.4.jar",
                new Uri("https://example.invalid/client.jar"),
                Convert.ToHexString(SHA1.HashData(content)),
                content.Length)],
            []);

    private sealed class StaticResponseHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }
}
