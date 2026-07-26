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

    [Fact]
    public async Task ExecuteAsync_FallsBackToOfficialSourceAfterMirrorFailure()
    {
        var content = "PCL Aurora mirror fallback"u8.ToArray();
        using var client = new HttpClient(new MirrorFailureHandler(content));
        var executor = new MinecraftDownloadExecutor(client);
        var plan = CreatePlan(content) with
        {
            Artifacts = [new MinecraftDownloadArtifact(
                "测试安装器",
                "cache/installer.jar",
                new Uri("https://bmclapi2.bangbang93.com/maven/example/installer.jar"),
                Convert.ToHexString(SHA1.HashData(content)),
                content.Length,
                [new Uri("https://official.example.invalid/installer.jar")])],
        };

        await executor.ExecuteAsync(plan, rootDirectory);

        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(rootDirectory, "cache", "installer.jar")));
    }

    [Fact]
    public async Task ExecuteAsync_DownloadsIndependentArtifactsWithBoundedConcurrency()
    {
        var content = "PCL Aurora concurrent downloads"u8.ToArray();
        var handler = new DelayedResponseHandler(content);
        using var client = new HttpClient(handler);
        var executor = new MinecraftDownloadExecutor(client);
        var artifacts = Enumerable.Range(0, 8)
            .Select(index => new MinecraftDownloadArtifact(
                $"测试文件 {index}",
                $"libraries/test/{index}.jar",
                new Uri($"https://example.invalid/{index}.jar"),
                Convert.ToHexString(SHA1.HashData(content)),
                content.Length))
            .ToArray();

        await executor.ExecuteAsync(new MinecraftDownloadPlan("test", artifacts, []), rootDirectory);

        Assert.InRange(handler.MaximumConcurrentRequests, 2, 4);
        Assert.All(artifacts, artifact => Assert.Equal(
            content,
            File.ReadAllBytes(Path.Combine(rootDirectory, artifact.RelativePath))));
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

    private sealed class MirrorFailureHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.Host == "bmclapi2.bangbang93.com"
                ? new HttpResponseMessage(HttpStatusCode.BadGateway)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }

    private sealed class DelayedResponseHandler(byte[] content) : HttpMessageHandler
    {
        private int activeRequests;
        private int maximumConcurrentRequests;

        public int MaximumConcurrentRequests => maximumConcurrentRequests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref activeRequests);
            SetMaximum(active);
            try
            {
                await Task.Delay(40, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
            }
            finally
            {
                Interlocked.Decrement(ref activeRequests);
            }
        }

        private void SetMaximum(int active)
        {
            int observed;
            while (active > (observed = maximumConcurrentRequests) &&
                   Interlocked.CompareExchange(ref maximumConcurrentRequests, active, observed) != observed)
            {
            }
        }
    }
}
