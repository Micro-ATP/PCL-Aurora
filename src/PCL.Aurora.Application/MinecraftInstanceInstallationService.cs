using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftInstanceInstallationService(
    IMinecraftVersionPreparationService versionPreparationService,
    IMinecraftAssetPreparationService assetPreparationService,
    IMinecraftDownloadExecutor downloadExecutor) : IMinecraftInstanceInstallationService
{
    public async Task InstallAsync(
        MinecraftInstance instance,
        IProgress<MinecraftInstallationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.Status != MinecraftInstanceStatus.Valid)
        {
            throw new InvalidOperationException("所选实例没有可读取的版本元数据，不能安装。");
        }

        var minecraftRootDirectory = GetMinecraftRootDirectory(instance);
        var versionPreparation = await versionPreparationService
            .PrepareAsync(instance, cancellationToken)
            .ConfigureAwait(false);
        if (!versionPreparation.DownloadPlan.IsReady)
        {
            throw new InvalidOperationException("游戏文件下载计划不完整：" + string.Join("；", versionPreparation.DownloadPlan.BlockingReasons));
        }

        progress?.Report(new(0, 2, "正在下载游戏文件、支持库和资源索引…"));
        await downloadExecutor
            .ExecuteAsync(versionPreparation.DownloadPlan, minecraftRootDirectory, cancellationToken)
            .ConfigureAwait(false);

        var assetPreparation = await assetPreparationService
            .PrepareAsync(instance, cancellationToken)
            .ConfigureAwait(false);
        if (!assetPreparation.DownloadPlan.IsReady)
        {
            throw new InvalidOperationException("资源对象下载计划不完整：" + string.Join("；", assetPreparation.DownloadPlan.BlockingReasons));
        }

        progress?.Report(new(1, 2, "正在下载资源对象…"));
        await downloadExecutor
            .ExecuteAsync(assetPreparation.DownloadPlan, minecraftRootDirectory, cancellationToken)
            .ConfigureAwait(false);
        progress?.Report(new(2, 2, "安装文件下载完成。资源映射将在启动游戏时准备。"));
    }

    private static string GetMinecraftRootDirectory(MinecraftInstance instance)
    {
        var versionsDirectory = Directory.GetParent(instance.DirectoryPath)?.FullName;
        var rootDirectory = versionsDirectory is null ? null : Directory.GetParent(versionsDirectory)?.FullName;
        return string.IsNullOrWhiteSpace(rootDirectory)
            ? throw new InvalidOperationException("无法确定 Minecraft 根目录。")
            : rootDirectory;
    }
}
