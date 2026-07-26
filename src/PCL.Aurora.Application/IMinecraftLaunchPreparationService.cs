using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftLaunchPreparationService
{
    Task<MinecraftLaunchPreparation> PrepareAsync(
        MinecraftInstance instance,
        MinecraftAccount? account,
        CancellationToken cancellationToken = default);
}
