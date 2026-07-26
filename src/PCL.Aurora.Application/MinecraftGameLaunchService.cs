using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftGameLaunchService(
    ILaunchReadinessService launchReadinessService,
    IMinecraftLaunchPreparationService launchPreparationService,
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
                new MinecraftGameLaunchRequestPreparation(null, readiness.BlockingReasons),
                readiness.BlockingReasons);
        }

        var launchPreparation = await launchPreparationService
            .PrepareAsync(instance, account, cancellationToken)
            .ConfigureAwait(false);
        var requestPreparation = MinecraftGameLaunchRequestBuilder.Prepare(instance, java, launchPreparation.ArgumentPreparation);
        var blockingReasons = readiness.BlockingReasons
            .Concat(launchPreparation.ClasspathInspection.BlockingReasons)
            .Concat(launchPreparation.ClasspathInspection.MissingFiles.Select(path => $"缺少文件：{path}"))
            .Concat(requestPreparation.BlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new(readiness, launchPreparation, requestPreparation, blockingReasons);
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

        return processRunner.StartAsync(preparation.RequestPreparation.Request, cancellationToken);
    }
}
