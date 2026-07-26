using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftLoaderInstallerService
{
    Task<MinecraftLoaderInstallerPlan> PrepareAsync(
        MinecraftLoaderCatalogEntry loader,
        string minecraftRootDirectory,
        JavaInstallation? java,
        CancellationToken cancellationToken = default);

    Task<MinecraftLoaderInstallerExecutionResult> InstallAsync(
        MinecraftLoaderInstallerPlan plan,
        string minecraftRootDirectory,
        bool hasExplicitUserConfirmation,
        CancellationToken cancellationToken = default);
}
