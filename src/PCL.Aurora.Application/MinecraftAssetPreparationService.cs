using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

public sealed class MinecraftAssetPreparationService(
    IMinecraftVersionPreparationService versionPreparationService,
    IMinecraftAssetIndexReader assetIndexReader) : IMinecraftAssetPreparationService
{
    public async Task<MinecraftAssetPreparation> PrepareAsync(
        MinecraftInstance instance,
        CancellationToken cancellationToken = default)
    {
        var versionPreparation = await versionPreparationService
            .PrepareAsync(instance, cancellationToken)
            .ConfigureAwait(false);
        var assetIndex = versionPreparation.Inspection.EffectiveMetadata?.AssetIndex;
        if (assetIndex is null)
        {
            var errors = new[] { "版本元数据未提供资源索引信息。" };
            return new(
                new(null, errors),
                new(null, [], errors),
                new(null, [], [], errors));
        }

        var inspection = await assetIndexReader
            .ReadAsync(instance, assetIndex.Id, cancellationToken)
            .ConfigureAwait(false);
        var versionsDirectory = Directory.GetParent(instance.DirectoryPath)?.FullName;
        var minecraftRootDirectory = versionsDirectory is null
            ? null
            : Directory.GetParent(versionsDirectory)?.FullName;
        return new(
            inspection,
            MinecraftAssetDownloadPlanBuilder.Create(inspection),
            MinecraftAssetMappingPlanBuilder.Build(inspection, minecraftRootDirectory, instance.DirectoryPath));
    }
}
