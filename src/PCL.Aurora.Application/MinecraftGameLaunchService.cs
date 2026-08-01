using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftGameLaunchService(
    ILaunchReadinessService launchReadinessService,
    IMinecraftLaunchPreparationService launchPreparationService,
    IMinecraftAssetPreparationService assetPreparationService,
    IAssetMapper assetMapper,
    INativeLibraryPreparer nativeLibraryPreparer,
    IGameProcessRunner processRunner,
    ILauncherPreferencesService? preferencesService = null,
    IMinecraftLaunchPatchService? launchPatchService = null) : IMinecraftGameLaunchService
{
    public async Task<MinecraftGameLaunchPreparation> PrepareAsync(
        MinecraftInstance? instance,
        MinecraftAccount? account,
        JavaInstallation? java,
        bool hasAcknowledgedAccountGuidance = false,
        CancellationToken cancellationToken = default)
    {
        var readiness = launchReadinessService.Evaluate(instance, account, java);
        var accountGuidance = MinecraftAccountLicenseGuidance.Evaluate(account);
        var guidanceBlockingReasons = accountGuidance.RequiresAcknowledgement && !hasAcknowledgedAccountGuidance
            ? new[] { "请先确认正版购买与上游赞助提示；该确认不能替代 Microsoft 正版认证。" }
            : [];
        if (instance is null || instance.Status != MinecraftInstanceStatus.Valid)
        {
            return new(
                readiness,
                accountGuidance,
                null,
                null,
                new MinecraftNativeLibraryPlan(string.Empty, [], [], readiness.BlockingReasons),
                new MinecraftGameLaunchRequestPreparation(null, readiness.BlockingReasons),
                readiness.BlockingReasons.Concat(guidanceBlockingReasons).ToArray());
        }

        var launchPreparation = await launchPreparationService
            .PrepareAsync(instance, account, java, cancellationToken)
            .ConfigureAwait(false);
        var assetPreparation = await assetPreparationService
            .PrepareAsync(instance, cancellationToken)
            .ConfigureAwait(false);
        var versionsDirectory = Directory.GetParent(instance.DirectoryPath)?.FullName;
        var minecraftRootDirectory = versionsDirectory is null
            ? null
            : Directory.GetParent(versionsDirectory)?.FullName;
        var nativeLibraryPlan = MinecraftNativeLibraryPlanBuilder.Build(
            launchPreparation.VersionPreparation.Inspection,
            minecraftRootDirectory,
            Path.Combine(instance.DirectoryPath, "natives"),
            java?.Architecture ?? JavaArchitecture.Unknown,
            launchPreparation.VersionPreparation.RuleEnvironment);
        var launchOptions = preferencesService?.Current.EffectiveLaunchOptions ?? MinecraftLaunchOptions.Default;
        var metadata = launchPreparation.VersionPreparation.Inspection.EffectiveMetadata;
        var requestPreparation = MinecraftGameLaunchRequestBuilder.Prepare(
            instance,
            java,
            launchPreparation.ArgumentPreparation,
            launchOptions,
            account);
        if (requestPreparation.Request is { } request &&
            metadata is not null &&
            java is not null &&
            launchPatchService is not null)
        {
            var patchPreparation = await launchPatchService
                .PrepareAsync(instance, metadata, java, launchOptions, request, cancellationToken)
                .ConfigureAwait(false);
            requestPreparation = new(
                patchPreparation.Request,
                patchPreparation.BlockingReasons);
        }
        var blockingReasons = readiness.BlockingReasons
            .Concat(guidanceBlockingReasons)
            .Concat(launchPreparation.ClasspathInspection.BlockingReasons)
            .Concat(launchPreparation.ClasspathInspection.MissingFiles.Select(path => $"缺少文件：{path}"))
            .Concat(assetPreparation.IndexInspection.Errors)
            .Concat(assetPreparation.MappingPlan.BlockingReasons)
            .Concat(assetPreparation.MappingPlan.MissingFiles.Select(path => $"缺少资源对象：{path}"))
            .Concat(nativeLibraryPlan.BlockingReasons)
            .Concat(nativeLibraryPlan.MissingFiles.Select(path => $"缺少 native 文件：{path}"))
            .Concat(launchPreparation.JavaRequirement?.GetBlockingReasons(java) ?? [])
            .Concat(launchPreparation.MemoryAllocation?.BlockingReasons ?? [])
            .Concat(requestPreparation.BlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new(readiness, accountGuidance, launchPreparation, assetPreparation, nativeLibraryPlan, requestPreparation, blockingReasons);
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
        if (preparation.AssetPreparation is null)
        {
            throw new InvalidOperationException("资源准备状态缺失。");
        }

        var assetMappingPreparation = await assetMapper
            .PrepareAsync(preparation.AssetPreparation.MappingPlan, cancellationToken)
            .ConfigureAwait(false);
        if (!assetMappingPreparation.IsReady)
        {
            throw new InvalidOperationException("资源尚未准备：" + string.Join("；", assetMappingPreparation.BlockingReasons));
        }

        var nativePreparation = await nativeLibraryPreparer
            .PrepareAsync(preparation.NativeLibraryPlan, cancellationToken)
            .ConfigureAwait(false);
        if (!nativePreparation.IsReady)
        {
            throw new InvalidOperationException("native 库尚未准备：" + string.Join("；", nativePreparation.BlockingReasons));
        }

        var preferences = preferencesService?.Current ?? LauncherPreferences.Default;
        if (preferences.EffectiveGameManagementOptions.AutoChangeGameLanguage &&
            preparation.LaunchPreparation?.VersionPreparation.Inspection.EffectiveMetadata is { } metadata)
        {
            await PclCeMinecraftOptionsUpdater.UpdateLanguageAsync(
                    preparation.RequestPreparation.Request!.WorkingDirectory,
                    metadata.ReleaseTime,
                    preferences.EffectiveLocalizationSettings.Language,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await processRunner
            .StartAsync(preparation.RequestPreparation.Request!, cancellationToken)
            .ConfigureAwait(false);
    }
}
