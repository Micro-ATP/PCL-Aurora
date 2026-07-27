using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftGameLaunchServiceTests
{
    [Fact]
    public async Task LaunchAsync_WhenPreparationIsBlocked_DoesNotCallProcessRunner()
    {
        var processRunner = new TrackingProcessRunner();
        var service = new MinecraftGameLaunchService(
            new LaunchReadinessService(),
            new UnusedLaunchPreparationService(),
            new UnusedAssetPreparationService(),
            new UnusedAssetMapper(),
            new UnusedNativeLibraryPreparer(),
            processRunner);

        var preparation = await service.PrepareAsync(instance: null, account: null, java: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(preparation));
        Assert.False(processRunner.WasCalled);
    }

    [Fact]
    public async Task LaunchAsync_WhenNativeArchiveIsMissing_DoesNotCallProcessRunner()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-native-launch-{Guid.NewGuid():N}");
        try
        {
            var instanceDirectory = Path.Combine(rootDirectory, "versions", "1.21.4");
            Directory.CreateDirectory(instanceDirectory);
            OfflineAccount.TryCreate("AuroraPlayer", out var account);
            var instance = new MinecraftInstance("1.21.4", instanceDirectory, "1.21.4", "release", null, MinecraftInstanceStatus.Valid);
            var java = new JavaInstallation("/usr/bin/java", "21", 21, "Test", JavaArchitecture.Arm64, JavaSource.Path, IsCompatible: true);
            var metadata = new MinecraftVersionMetadata(
                "1.21.4",
                null,
                "release",
                null,
                null,
                null,
                null,
                [new MinecraftVersionLibrary(
                    "org.example:native:1.0",
                    null,
                    null,
                    HasConditionalRules: false,
                    NativeClassifiers: new Dictionary<string, string> { ["osx"] = "natives-macos-${arch}" },
                    Classifiers: new Dictionary<string, MinecraftVersionLibraryClassifier>
                    {
                        ["natives-macos-arm64"] = new(
                            "org/example/native/1.0/native-arm64.jar",
                            new MinecraftVersionDownload(new Uri("https://example.invalid/native.jar"), null, null)),
                    })]);
            var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);
            var launchPreparation = new MinecraftLaunchPreparation(
                new MinecraftVersionPreparation(inspection, new MinecraftDownloadPlan("1.21.4", [], [])),
                new MinecraftClasspathInspection(["/libraries/example.jar"], [], []),
                new MinecraftLaunchArgumentPreparation(new MinecraftLaunchArguments([], "example.Main", []), []));
            var processRunner = new TrackingProcessRunner();
            var launchPreparationService = new FixedLaunchPreparationService(launchPreparation);
            var service = new MinecraftGameLaunchService(
                new LaunchReadinessService(),
                launchPreparationService,
                new FixedAssetPreparationService(CreateReadyAssetPreparation()),
                new UnusedAssetMapper(),
                new UnusedNativeLibraryPreparer(),
                processRunner);

            var preparation = await service.PrepareAsync(instance, account, java);

            Assert.False(preparation.CanLaunch);
            Assert.Contains(preparation.BlockingReasons, reason => reason.Contains("正版购买", StringComparison.Ordinal));
            Assert.Contains(preparation.BlockingReasons, reason => reason.Contains("缺少 native 文件", StringComparison.Ordinal));

            var acknowledgedPreparation = await service.PrepareAsync(
                instance,
                account,
                java,
                hasAcknowledgedAccountGuidance: true);

            Assert.DoesNotContain(
                acknowledgedPreparation.BlockingReasons,
                reason => reason.Contains("正版购买", StringComparison.Ordinal));
            Assert.Same(java, launchPreparationService.RequestedJava);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(preparation));
            Assert.False(processRunner.WasCalled);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PrepareAsync_RejectsJavaThatDoesNotMeetVersionRequirement()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-java-requirement-{Guid.NewGuid():N}");
        try
        {
            var instanceDirectory = Path.Combine(rootDirectory, "versions", "1.21.4");
            Directory.CreateDirectory(instanceDirectory);
            OfflineAccount.TryCreate("AuroraPlayer", out var account);
            var instance = new MinecraftInstance("1.21.4", instanceDirectory, "1.21.4", "release", null, MinecraftInstanceStatus.Valid);
            var java = new JavaInstallation("/usr/bin/java", "17", 17, "Test", JavaArchitecture.Arm64, JavaSource.Path, IsCompatible: true);
            var metadata = new MinecraftVersionMetadata("1.21.4", null, "release", null, null, null);
            var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);
            var launchPreparation = new MinecraftLaunchPreparation(
                new MinecraftVersionPreparation(inspection, new MinecraftDownloadPlan("1.21.4", [], [])),
                new MinecraftClasspathInspection(["/libraries/example.jar"], [], []),
                new MinecraftLaunchArgumentPreparation(new MinecraftLaunchArguments([], "example.Main", []), []),
                new MinecraftJavaRequirement(21, null, null, "test"));
            var launchPreparationService = new FixedLaunchPreparationService(launchPreparation);
            var service = new MinecraftGameLaunchService(
                new LaunchReadinessService(),
                launchPreparationService,
                new FixedAssetPreparationService(CreateReadyAssetPreparation()),
                new UnusedAssetMapper(),
                new UnusedNativeLibraryPreparer(),
                new TrackingProcessRunner());

            var preparation = await service.PrepareAsync(instance, account, java, hasAcknowledgedAccountGuidance: true);

            Assert.False(preparation.CanLaunch);
            Assert.Contains(preparation.BlockingReasons, reason => reason.Contains("低于", StringComparison.Ordinal));
            Assert.Same(java, launchPreparationService.RequestedJava);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    private sealed class UnusedLaunchPreparationService : IMinecraftLaunchPreparationService
    {
        public Task<MinecraftLaunchPreparation> PrepareAsync(
            MinecraftInstance instance,
            MinecraftAccount? account,
            JavaInstallation? java = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("无效实例不应请求启动参数。");
    }

    private sealed class TrackingProcessRunner : IGameProcessRunner
    {
        public bool WasCalled { get; private set; }

        public Task<GameProcessSession> StartAsync(
            MinecraftGameLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("阻断状态不应启动进程。");
        }
    }

    private sealed class UnusedNativeLibraryPreparer : INativeLibraryPreparer
    {
        public Task<MinecraftNativeLibraryPreparation> PrepareAsync(
            MinecraftNativeLibraryPlan nativeLibraryPlan,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("无效实例不应准备 native 库。");
    }

    private sealed class UnusedAssetMapper : IAssetMapper
    {
        public Task<MinecraftAssetMappingPreparation> PrepareAsync(
            MinecraftAssetMappingPlan mappingPlan,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("阻断状态不应映射资源。");
    }

    private sealed class UnusedAssetPreparationService : IMinecraftAssetPreparationService
    {
        public Task<MinecraftAssetPreparation> PrepareAsync(
            MinecraftInstance instance,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("无效实例不应读取资源索引。");
    }

    private sealed class FixedLaunchPreparationService(MinecraftLaunchPreparation preparation) : IMinecraftLaunchPreparationService
    {
        public JavaInstallation? RequestedJava { get; private set; }

        public Task<MinecraftLaunchPreparation> PrepareAsync(
            MinecraftInstance instance,
            MinecraftAccount? account,
            JavaInstallation? java = null,
            CancellationToken cancellationToken = default)
        {
            RequestedJava = java;
            return Task.FromResult(preparation);
        }
    }

    private sealed class FixedAssetPreparationService(MinecraftAssetPreparation preparation) : IMinecraftAssetPreparationService
    {
        public Task<MinecraftAssetPreparation> PrepareAsync(
            MinecraftInstance instance,
            CancellationToken cancellationToken = default) => Task.FromResult(preparation);
    }

    private static MinecraftAssetPreparation CreateReadyAssetPreparation()
    {
        var index = new MinecraftAssetIndex("17", [], IsVirtual: false, MapsToResources: false);
        var inspection = new MinecraftAssetIndexParseResult(index, []);
        return new(inspection, new("17", [], []), new(null, [], [], []));
    }
}
