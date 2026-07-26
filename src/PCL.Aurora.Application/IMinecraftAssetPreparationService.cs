using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftAssetPreparationService
{
    Task<MinecraftAssetPreparation> PrepareAsync(
        MinecraftInstance instance,
        CancellationToken cancellationToken = default);
}
