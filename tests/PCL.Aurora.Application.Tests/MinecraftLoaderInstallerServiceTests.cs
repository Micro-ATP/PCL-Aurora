using System.Net;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftLoaderInstallerServiceTests
{
    [Fact]
    public async Task PrepareAsync_ObtainsOfficialChecksumBeforeAllowingMirrorDownload()
    {
        using var client = new HttpClient(new ChecksumHandler("0123456789abcdef0123456789abcdef01234567"));
        var service = new MinecraftLoaderInstallerService(client, new UnusedDownloadExecutor(), new UnusedProcessRunner());

        var plan = await service.PrepareAsync(CreateForge(), "/tmp/pcl-aurora-loader", CreateJava());

        Assert.True(plan.CanInstall);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", plan.InstallerArtifact!.Sha1);
        Assert.Equal("bmclapi2.bangbang93.com", plan.InstallerArtifact.Url.Host);
    }

    [Fact]
    public async Task PrepareAsync_InvalidOfficialChecksum_BlocksExecution()
    {
        using var client = new HttpClient(new ChecksumHandler("not-a-sha1"));
        var service = new MinecraftLoaderInstallerService(client, new UnusedDownloadExecutor(), new UnusedProcessRunner());

        var plan = await service.PrepareAsync(CreateForge(), "/tmp/pcl-aurora-loader", CreateJava());

        Assert.False(plan.CanInstall);
        Assert.Contains(plan.BlockingReasons, reason => reason.Contains("校验", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstallAsync_WithoutConfirmation_DoesNotDownloadOrStartProcess()
    {
        var download = new TrackingDownloadExecutor();
        var process = new TrackingProcessRunner();
        using var client = new HttpClient(new ChecksumHandler("0123456789abcdef0123456789abcdef01234567"));
        var service = new MinecraftLoaderInstallerService(client, download, process);
        var plan = await service.PrepareAsync(CreateForge(), "/tmp/pcl-aurora-loader", CreateJava());

        var result = await service.InstallAsync(plan, "/tmp/pcl-aurora-loader", hasExplicitUserConfirmation: false);

        Assert.False(result.Succeeded);
        Assert.False(download.WasCalled);
        Assert.False(process.WasCalled);
    }

    private static MinecraftLoaderCatalogEntry CreateForge() => new(
        MinecraftLoaderKind.Forge,
        "1.20.1",
        "47.2.0",
        MinecraftLoaderChannel.Release,
        false,
        new PclCeForgeVersionEntry("47.2.0", null, "1.20.1"));

    private static JavaInstallation CreateJava() =>
        new("/usr/bin/java", "21", 21, "Test", JavaArchitecture.Arm64, JavaSource.Path, true);

    private sealed class ChecksumHandler(string checksum) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("maven.minecraftforge.net", request.RequestUri!.Host);
            Assert.EndsWith(".jar.sha1", request.RequestUri.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(checksum) });
        }
    }

    private sealed class UnusedDownloadExecutor : IMinecraftDownloadExecutor
    {
        public Task ExecuteAsync(MinecraftDownloadPlan downloadPlan, string minecraftRootDirectory, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("此测试不应下载。");

        public Task ExecuteAsync(MinecraftAssetDownloadPlan downloadPlan, string minecraftRootDirectory, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("此测试不应下载。");
    }

    private sealed class TrackingDownloadExecutor : IMinecraftDownloadExecutor
    {
        public bool WasCalled { get; private set; }

        public Task ExecuteAsync(MinecraftDownloadPlan downloadPlan, string minecraftRootDirectory, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(MinecraftAssetDownloadPlan downloadPlan, string minecraftRootDirectory, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("此测试不应下载资源。");
    }

    private sealed class UnusedProcessRunner : IMinecraftLoaderInstallerProcessRunner
    {
        public Task<MinecraftLoaderInstallerExecutionResult> ExecuteAsync(MinecraftLoaderInstallerProcessRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("此测试不应启动安装器。");
    }

    private sealed class TrackingProcessRunner : IMinecraftLoaderInstallerProcessRunner
    {
        public bool WasCalled { get; private set; }

        public Task<MinecraftLoaderInstallerExecutionResult> ExecuteAsync(MinecraftLoaderInstallerProcessRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new MinecraftLoaderInstallerExecutionResult(0, [], []));
        }
    }
}
