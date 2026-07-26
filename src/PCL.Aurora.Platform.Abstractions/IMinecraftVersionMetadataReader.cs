using PCL.Aurora.Domain;

namespace PCL.Aurora.Platform.Abstractions;

public interface IMinecraftVersionMetadataReader
{
    Task<MinecraftVersionMetadataInspection> InspectAsync(
        MinecraftInstance instance,
        CancellationToken cancellationToken = default);
}
