using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftVersionPreparationService
{
    Task<MinecraftVersionPreparation> PrepareAsync(
        MinecraftInstance instance,
        CancellationToken cancellationToken = default);
}
