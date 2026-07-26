using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftInstanceInstallationService
{
    Task InstallAsync(
        MinecraftInstance instance,
        IProgress<MinecraftInstallationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
