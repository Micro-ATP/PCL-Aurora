using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftDownloadExecutor
{
    Task ExecuteAsync(
        MinecraftDownloadPlan downloadPlan,
        string minecraftRootDirectory,
        CancellationToken cancellationToken = default);
}
