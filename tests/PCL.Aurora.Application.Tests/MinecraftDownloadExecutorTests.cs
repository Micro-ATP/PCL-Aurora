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

    [Fact]
    public async Task ExecuteAsync_ReportsReceivedBytesAndVerifiedArtifactCount()
    {
        var content = "PCL Aurora progress"u8.ToArray();
        using var client = new HttpClient(new StaticResponseHandler(content));
        var executor = new MinecraftDownloadExecutor(client);
        var updates = new List<MinecraftDownloadProgress>();

        await executor.ExecuteAsync(CreatePlan(content), rootDirectory, new InlineProgress<MinecraftDownloadProgress>(updates.Add));

        Assert.Contains(updates, update => update.ActiveArtifacts == 1 && update.DownloadedBytes == 0);
        var completed = Assert.Single(updates, update => update.CompletedArtifacts == 1);
        Assert.Equal(1, completed.TotalArtifacts);
        Assert.Equal(content.Length, completed.DownloadedBytes);
        Assert.Equal(content.Length, completed.TotalBytes);
        Assert.Equal(0, completed.ActiveArtifacts);
    }

    [Fact]
    public async Task ExecuteAsync_ForVerifiedLargeFile_UsesValidatedParallelRanges()
    {
        var content = Enumerable.Range(0, 2 * 1024 * 1024).Select(index => (byte)(index % 251)).ToArray();
        var handler = new RangeResponseHandler(content);
        using var client = new HttpClient(handler);
        var executor = new MinecraftDownloadExecutor(client);

        await executor.ExecuteAsync(CreatePlan(content), rootDirectory);

        Assert.True(handler.ProbeRequests >= 1);
        Assert.True(handler.RangeRequests >= 2, $"实际分片请求数：{handler.RangeRequests}");
        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(rootDirectory, "versions", "1.21.4", "1.21.4.jar")));
    }

    [Fact]
    public async Task ExecuteAsync_ForLargeFile_LimitsParallelRangesPerArtifactToFour()
    {
        var content = Enumerable.Range(0, 8 * 1024 * 1024).Select(index => (byte)(index % 251)).ToArray();
        var handler = new RangeResponseHandler(content);
        using var client = new HttpClient(handler);
        var preferencesService = new LauncherPreferencesService(new StaticPreferencesStore(
            new LauncherPreferences(LauncherThemeMode.System, DownloadConcurrency: LauncherDownloadSettings.MaximumConcurrency)));
        await preferencesService.LoadAsync();
        var executor = new MinecraftDownloadExecutor(client, preferencesService);

        await executor.ExecuteAsync(CreatePlan(content), rootDirectory);

        Assert.Equal(4, handler.RangeRequests);
        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(rootDirectory, "versions", "1.21.4", "1.21.4.jar")));
    }

    [Fact]
    public async Task ExecuteAsync_WhenServerRejectsRange_FallsBackToSingleConnection()
    {
        var content = Enumerable.Repeat((byte)0x3a, 2 * 1024 * 1024).ToArray();
        var handler = new RangeRejectedHandler(content);
        using var client = new HttpClient(handler);
        var executor = new MinecraftDownloadExecutor(client);

        await executor.ExecuteAsync(CreatePlan(content), rootDirectory);

        Assert.Equal(1, handler.RangeRequests);
        Assert.Equal(1, handler.SingleConnectionRequests);
        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(rootDirectory, "versions", "1.21.4", "1.21.4.jar")));
    }

    [Fact]
    public async Task ExecuteAsync_WhenParallelRangeHashFails_PreservesExistingFileAndCleansPartialFile()
    {
        var content = Enumerable.Repeat((byte)0x7f, 2 * 1024 * 1024).ToArray();
        using var client = new HttpClient(new RangeResponseHandler(content));
        var executor = new MinecraftDownloadExecutor(client);
        var destinationPath = Path.Combine(rootDirectory, "versions", "1.21.4", "1.21.4.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await File.WriteAllTextAsync(destinationPath, "existing content");
        var invalidPlan = CreatePlan(content) with
        {
            Artifacts = [new MinecraftDownloadArtifact(
                "Minecraft 客户端",
                "versions/1.21.4/1.21.4.jar",
                new Uri("https://example.invalid/client.jar"),
                new string('0', 40),
                content.Length)],
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => executor.ExecuteAsync(invalidPlan, rootDirectory));

        Assert.Equal("existing content", await File.ReadAllTextAsync(destinationPath));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(destinationPath)!, "*.partial"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenParallelRangeIsCancelled_CleansPartialFile()
    {
        var content = Enumerable.Repeat((byte)0x61, 2 * 1024 * 1024).ToArray();
        using var client = new HttpClient(new DelayedRangeResponseHandler(content));
        var executor = new MinecraftDownloadExecutor(client);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(CreatePlan(content), rootDirectory, cancellation.Token));

        var destinationDirectory = Path.Combine(rootDirectory, "versions", "1.21.4");
        Assert.False(File.Exists(Path.Combine(destinationDirectory, "1.21.4.jar")));
        Assert.False(Directory.Exists(destinationDirectory) && Directory.EnumerateFiles(destinationDirectory, "*.partial").Any());
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

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class StaticPreferencesStore(LauncherPreferences preferences) : ILauncherPreferencesStore
    {
        public Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LauncherPreferencesLoadResult(preferences, null));

        public Task SaveAsync(LauncherPreferences savedPreferences, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

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

    private sealed class RangeResponseHandler(byte[] content) : HttpMessageHandler
    {
        private int probeRequests;
        private int rangeRequests;

        public int ProbeRequests => probeRequests;

        public int RangeRequests => rangeRequests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.SingleOrDefault();
            if (range is null || range.From is null || range.To is null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
            }

            if (range.From == 0 && range.To == 0)
            {
                Interlocked.Increment(ref probeRequests);
            }
            else
            {
                Interlocked.Increment(ref rangeRequests);
            }

            var start = checked((int)range.From.Value);
            var end = checked((int)range.To.Value);
            var responseContent = new ByteArrayContent(content[start..(end + 1)]);
            responseContent.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(start, end, content.Length);
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = responseContent };
            response.Headers.AcceptRanges.Add("bytes");
            return Task.FromResult(response);
        }
    }

    private sealed class RangeRejectedHandler(byte[] content) : HttpMessageHandler
    {
        public int RangeRequests { get; private set; }

        public int SingleConnectionRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Range is not null)
            {
                RangeRequests++;
            }
            else
            {
                SingleConnectionRequests++;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        }
    }

    private sealed class DelayedRangeResponseHandler(byte[] content) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.SingleOrDefault();
            if (range is { From: 0, To: 0 })
            {
                var responseContent = new ByteArrayContent(content[..1]);
                responseContent.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(0, 0, content.Length);
                var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = responseContent };
                response.Headers.AcceptRanges.Add("bytes");
                return response;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("取消的请求不应返回响应。");
        }
    }
}
