using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftVersionProvisioningService
{
    Task<MinecraftInstance> ProvisionAsync(
        MinecraftVersionCatalogEntry version,
        CancellationToken cancellationToken = default);
}
