using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

public sealed class InstanceCatalogService(
    IMinecraftInstanceLocator instanceLocator,
    ILauncherPreferencesService? preferencesService = null) : IInstanceCatalogService
{
    public Task<IReadOnlyList<MinecraftInstance>> GetAllAsync(CancellationToken cancellationToken = default) =>
        instanceLocator.FindAllAsync(
            preferencesService?.Current.EffectiveMinecraftRootDirectories ?? [],
            cancellationToken);
}
