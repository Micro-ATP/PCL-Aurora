using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IAssetMapper
{
    Task<MinecraftAssetMappingPreparation> PrepareAsync(
        MinecraftAssetMappingPlan mappingPlan,
        CancellationToken cancellationToken = default);
}
