using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftDownloadExecutor
{
    Task ExecuteAsync(
        MinecraftDownloadPlan downloadPlan,
        string minecraftRootDirectory,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        MinecraftDownloadPlan downloadPlan,
        string minecraftRootDirectory,
        IProgress<MinecraftDownloadProgress>? progress,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(downloadPlan, minecraftRootDirectory, cancellationToken);

    Task ExecuteAsync(
        MinecraftAssetDownloadPlan downloadPlan,
        string minecraftRootDirectory,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        MinecraftAssetDownloadPlan downloadPlan,
        string minecraftRootDirectory,
        IProgress<MinecraftDownloadProgress>? progress,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(downloadPlan, minecraftRootDirectory, cancellationToken);
}
