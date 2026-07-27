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

    [Fact]
    public async Task InstallAsync_LegacyOptiFine_CreatesAtomicInheritedVersionWithoutRunningJava()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-aurora-legacy-optifine-" + Guid.NewGuid().ToString("N"));
        try
        {
            var baseDirectory = Path.Combine(root, "versions", "1.12.2");
            Directory.CreateDirectory(baseDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(baseDirectory, "1.12.2.json"),
                """
                {
                  "id": "1.12.2",
                  "mainClass": "net.minecraft.client.main.Main",
                  "minecraftArguments": "--username ${auth_player_name} --gameDir ${game_directory}"
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(baseDirectory, "1.12.2.jar"), "base-client");
            var download = new LegacyArtifactDownloadExecutor();
            var process = new TrackingProcessRunner();
            using var client = new HttpClient(new UnusedHandler());
            var service = new MinecraftLoaderInstallerService(client, download, process);
            var loader = new MinecraftLoaderCatalogEntry(
                MinecraftLoaderKind.OptiFine,
                "1.12.2",
                "C9",
                MinecraftLoaderChannel.Release,
                true,
                null,
                new("OptiFine_1.12.2_HD_U_C9.jar", "HD_U", "C9", false, null));

            var plan = await service.PrepareAsync(loader, root, java: null);
            var result = await service.InstallAsync(plan, root, hasExplicitUserConfirmation: true);

            Assert.True(plan.CanInstall);
            Assert.True(result.Succeeded);
            Assert.True(download.WasCalled);
            Assert.False(process.WasCalled);
            var versionDirectory = Path.Combine(root, "versions", "1.12.2-OptiFine_HD_U_C9");
            Assert.Equal("base-client", await File.ReadAllTextAsync(Path.Combine(versionDirectory, "1.12.2-OptiFine_HD_U_C9.jar")));
            var metadata = MinecraftVersionMetadataParser.Parse(
                await File.ReadAllTextAsync(Path.Combine(versionDirectory, "1.12.2-OptiFine_HD_U_C9.json")));
            Assert.True(metadata.IsSuccess);
            Assert.Equal("net.minecraft.launchwrapper.Launch", metadata.Metadata!.Launch!.MainClass);
            Assert.Contains("--username ${auth_player_name}", metadata.Metadata.Launch.LegacyGameArguments, StringComparison.Ordinal);
            Assert.Contains("--tweakClass optifine.OptiFineTweaker", metadata.Metadata.Launch.LegacyGameArguments, StringComparison.Ordinal);
            Assert.Contains(metadata.Metadata.Libraries!, library => library.Name == "optifine:OptiFine:1.12.2_HD_U_C9");
            Assert.True(File.Exists(Path.Combine(root, "libraries", "optifine", "OptiFine", "1.12.2_HD_U_C9", "OptiFine-1.12.2_HD_U_C9.jar")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PrepareAsync_LegacyOptiFine_RejectsSymbolicLinkAtLibraryTarget()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-aurora-legacy-optifine-link-" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "pcl-aurora-legacy-optifine-outside-" + Guid.NewGuid().ToString("N"));
        try
        {
            var baseDirectory = Path.Combine(root, "versions", "1.12.2");
            Directory.CreateDirectory(baseDirectory);
            Directory.CreateDirectory(outside);
            await File.WriteAllTextAsync(
                Path.Combine(baseDirectory, "1.12.2.json"),
                """{ "id": "1.12.2", "mainClass": "net.minecraft.client.main.Main", "minecraftArguments": "--username ${auth_player_name}" }""");
            await File.WriteAllTextAsync(Path.Combine(baseDirectory, "1.12.2.jar"), "base-client");
            Directory.CreateSymbolicLink(Path.Combine(root, "libraries"), outside);
            using var client = new HttpClient(new UnusedHandler());
            var service = new MinecraftLoaderInstallerService(client, new UnusedDownloadExecutor(), new UnusedProcessRunner());
            var loader = new MinecraftLoaderCatalogEntry(
                MinecraftLoaderKind.OptiFine,
                "1.12.2",
                "C9",
                MinecraftLoaderChannel.Release,
                true,
                null,
                new("OptiFine_1.12.2_HD_U_C9.jar", "HD_U", "C9", false, null));

            var plan = await service.PrepareAsync(loader, root, java: null);

            Assert.False(plan.CanInstall);
            Assert.Contains(plan.BlockingReasons, reason => reason.Contains("符号链接", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            if (Directory.Exists(outside))
            {
                Directory.Delete(outside, recursive: true);
            }
        }
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

    private sealed class LegacyArtifactDownloadExecutor : IMinecraftDownloadExecutor
    {
        public bool WasCalled { get; private set; }

        public async Task ExecuteAsync(MinecraftDownloadPlan downloadPlan, string minecraftRootDirectory, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            var artifact = Assert.Single(downloadPlan.Artifacts);
            var path = Path.Combine(minecraftRootDirectory, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, new byte[300 * 1024], cancellationToken);
        }

        public Task ExecuteAsync(MinecraftAssetDownloadPlan downloadPlan, string minecraftRootDirectory, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("此测试不应下载资源。 ");
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

    private sealed class UnusedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("旧版 OptiFine 准备不应请求安装器校验。 ");
    }
}
