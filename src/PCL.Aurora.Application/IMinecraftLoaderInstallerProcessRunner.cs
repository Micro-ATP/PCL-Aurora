using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftLoaderInstallerProcessRunner
{
    Task<MinecraftLoaderInstallerExecutionResult> ExecuteAsync(
        MinecraftLoaderInstallerProcessRequest request,
        CancellationToken cancellationToken = default);
}
