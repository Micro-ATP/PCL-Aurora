using PCL.Aurora.Domain;

namespace PCL.Aurora.Platform.Abstractions;

public interface IMinecraftAssetIndexReader
{
    Task<MinecraftAssetIndexParseResult> ReadAsync(
        MinecraftInstance instance,
        string assetIndexId,
        CancellationToken cancellationToken = default);
}
