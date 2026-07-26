using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftGameLaunchService(
    ILaunchReadinessService launchReadinessService,
    IMinecraftLaunchPreparationService launchPreparationService,
    INativeLibraryPreparer nativeLibraryPreparer,
    IGameProcessRunner processRunner) : IMinecraftGameLaunchService
{
    public async Task<MinecraftGameLaunchPreparation> PrepareAsync(
        MinecraftInstance? instance,
        MinecraftAccount? account,
        JavaInstallation? java,
        CancellationToken cancellationToken = default)
    {
        var readiness = launchReadinessService.Evaluate(instance, account, java);
        if (instance is null || instance.Status != MinecraftInstanceStatus.Valid)
        {
            return new(
                readiness,
                null,
                new MinecraftNativeLibraryPlan(string.Empty, [], [], readiness.BlockingReasons),
                new MinecraftGameLaunchRequestPreparation(null, readiness.BlockingReasons),
                readiness.BlockingReasons);
        }

        var launchPreparation = await launchPreparationService
            .PrepareAsync(instance, account, cancellationToken)
            .ConfigureAwait(false);
        var versionsDirectory = Directory.GetParent(instance.DirectoryPath)?.FullName;
        var minecraftRootDirectory = versionsDirectory is null
            ? null
            : Directory.GetParent(versionsDirectory)?.FullName;
        var nativeLibraryPlan = MinecraftNativeLibraryPlanBuilder.Build(
            launchPreparation.VersionPreparation.Inspection,
            minecraftRootDirectory,
            Path.Combine(instance.DirectoryPath, "natives"),
            java?.Architecture ?? JavaArchitecture.Unknown);
        var requestPreparation = MinecraftGameLaunchRequestBuilder.Prepare(instance, java, launchPreparation.ArgumentPreparation);
        var blockingReasons = readiness.BlockingReasons
            .Concat(launchPreparation.ClasspathInspection.BlockingReasons)
            .Concat(launchPreparation.ClasspathInspection.MissingFiles.Select(path => $"缺少文件：{path}"))
            .Concat(nativeLibraryPlan.BlockingReasons)
            .Concat(nativeLibraryPlan.MissingFiles.Select(path => $"缺少 native 文件：{path}"))
            .Concat(requestPreparation.BlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new(readiness, launchPreparation, nativeLibraryPlan, requestPreparation, blockingReasons);
    }

    public Task<GameProcessSession> LaunchAsync(
        MinecraftGameLaunchPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (!preparation.CanLaunch || preparation.RequestPreparation.Request is null)
        {
            throw new InvalidOperationException("游戏启动条件尚未满足。");
        }

        return LaunchPreparedAsync(preparation, cancellationToken);
    }

    private async Task<GameProcessSession> LaunchPreparedAsync(
        MinecraftGameLaunchPreparation preparation,
        CancellationToken cancellationToken)
    {
        var nativePreparation = await nativeLibraryPreparer
            .PrepareAsync(preparation.NativeLibraryPlan, cancellationToken)
            .ConfigureAwait(false);
        if (!nativePreparation.IsReady)
        {
            throw new InvalidOperationException("native 库尚未准备：" + string.Join("；", nativePreparation.BlockingReasons));
        }

        return await processRunner
            .StartAsync(preparation.RequestPreparation.Request!, cancellationToken)
            .ConfigureAwait(false);
    }
}
