using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftInstanceInstallationServiceTests
{
    [Fact]
    public async Task InstallAsync_ExecutesGamePlanThenAssetPlanOnlyForValidInstance()
    {
        var instance = new MinecraftInstance(
            "1.21.4",
            Path.Combine(Path.GetTempPath(), "pcl-aurora-install", "versions", "1.21.4"),
            "1.21.4",
            "release",
            null,
            MinecraftInstanceStatus.Valid);
        var executor = new TrackingExecutor();
        var service = new MinecraftInstanceInstallationService(
            new FixedVersionPreparationService(CreateVersionPreparation()),
            new FixedAssetPreparationService(CreateAssetPreparation()),
            executor);
        var updates = new List<MinecraftInstallationProgress>();

        await service.InstallAsync(instance, new InlineProgress<MinecraftInstallationProgress>(updates.Add));

        Assert.Equal(["game", "assets"], executor.ExecutedPlans);
        Assert.Equal([0, 1, 2], updates.Select(update => update.CompletedStages));
    }

    [Fact]
    public async Task InstallAsync_ForwardsTrustworthyDownloadProgressForEachStage()
    {
        var instance = new MinecraftInstance(
            "1.21.4",
            Path.Combine(Path.GetTempPath(), "pcl-aurora-install", "versions", "1.21.4"),
            "1.21.4",
            "release",
            null,
            MinecraftInstanceStatus.Valid);
        var service = new MinecraftInstanceInstallationService(
            new FixedVersionPreparationService(CreateVersionPreparation()),
            new FixedAssetPreparationService(CreateAssetPreparation()),
            new ProgressReportingExecutor());
        var updates = new List<MinecraftInstallationProgress>();

        await service.InstallAsync(instance, new InlineProgress<MinecraftInstallationProgress>(updates.Add));

        Assert.Contains(updates, update => update.CompletedStages == 0 && update.TotalArtifacts == 2 && update.DownloadedBytes == 64);
        Assert.Contains(updates, update => update.CompletedStages == 1 && update.TotalArtifacts == 3 && update.DownloadedBytes == 96);
    }

    private static MinecraftVersionPreparation CreateVersionPreparation()
    {
        var metadata = new MinecraftVersionMetadata("1.21.4", null, "release", null, null, null);
        var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);
        return new(inspection, new("1.21.4", [], []));
    }

    private static MinecraftAssetPreparation CreateAssetPreparation()
    {
        var index = new MinecraftAssetIndex("17", [], false, false);
        return new(new(index, []), new("17", [], []), new(null, [], [], []));
    }

    private sealed class FixedVersionPreparationService(MinecraftVersionPreparation preparation) : IMinecraftVersionPreparationService
    {
        public Task<MinecraftVersionPreparation> PrepareAsync(MinecraftInstance instance, CancellationToken cancellationToken = default) => Task.FromResult(preparation);
    }

    private sealed class FixedAssetPreparationService(MinecraftAssetPreparation preparation) : IMinecraftAssetPreparationService
    {
        public Task<MinecraftAssetPreparation> PrepareAsync(MinecraftInstance instance, CancellationToken cancellationToken = default) => Task.FromResult(preparation);
    }

    private sealed class TrackingExecutor : IMinecraftDownloadExecutor
    {
        public List<string> ExecutedPlans { get; } = [];

        public Task ExecuteAsync(MinecraftDownloadPlan downloadPlan, string minecraftRootDirectory, CancellationToken cancellationToken = default)
        {
            ExecutedPlans.Add("game");
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(MinecraftAssetDownloadPlan downloadPlan, string minecraftRootDirectory, CancellationToken cancellationToken = default)
        {
            ExecutedPlans.Add("assets");
            return Task.CompletedTask;
        }
    }

    private sealed class ProgressReportingExecutor : IMinecraftDownloadExecutor
    {
        public Task ExecuteAsync(MinecraftDownloadPlan downloadPlan, string minecraftRootDirectory, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ExecuteAsync(MinecraftAssetDownloadPlan downloadPlan, string minecraftRootDirectory, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ExecuteAsync(
            MinecraftDownloadPlan downloadPlan,
            string minecraftRootDirectory,
            IProgress<MinecraftDownloadProgress>? progress,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(new(1, 2, 1, 64, 128, "正在下载游戏文件…"));
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(
            MinecraftAssetDownloadPlan downloadPlan,
            string minecraftRootDirectory,
            IProgress<MinecraftDownloadProgress>? progress,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(new(2, 3, 1, 96, 192, "正在下载资源对象…"));
            return Task.CompletedTask;
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
