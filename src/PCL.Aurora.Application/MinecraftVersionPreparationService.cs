using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

public sealed class MinecraftVersionPreparationService(IMinecraftVersionMetadataReader metadataReader)
    : IMinecraftVersionPreparationService
{
    public async Task<MinecraftVersionPreparation> PrepareAsync(
        MinecraftInstance instance,
        CancellationToken cancellationToken = default)
    {
        var inspection = await metadataReader.InspectAsync(instance, cancellationToken).ConfigureAwait(false);
        return new(inspection, MinecraftDownloadPlanBuilder.Create(inspection.EffectiveMetadata));
    }
}
