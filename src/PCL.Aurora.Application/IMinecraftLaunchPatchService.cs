using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftLaunchPatchService
{
    Task<MinecraftLaunchPatchPreparation> PrepareAsync(
        MinecraftInstance instance,
        MinecraftVersionMetadata metadata,
        JavaInstallation java,
        MinecraftLaunchOptions options,
        MinecraftGameLaunchRequest request,
        CancellationToken cancellationToken = default);
}
