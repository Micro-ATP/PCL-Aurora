namespace PCL.Aurora.Domain;

public static class MinecraftAssetDownloadPlanBuilder
{
    public static MinecraftAssetDownloadPlan Create(MinecraftAssetIndexParseResult inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (!inspection.IsSuccess || inspection.Index is null)
        {
            return new(null, [], inspection.Errors.Count > 0 ? inspection.Errors : ["资源索引无效。"]);
        }

        var artifacts = inspection.Index.Objects
            .DistinctBy(asset => asset.Hash, StringComparer.OrdinalIgnoreCase)
            .Select(asset => Pcl2VerifiedMirrorSourceMapper.PreferMirrorWhenVerified(
                new MinecraftDownloadArtifact(
                    $"资源对象 {asset.Name}",
                    $"assets/objects/{asset.Hash[..2]}/{asset.Hash}",
                    new Uri($"https://resources.download.minecraft.net/{asset.Hash[..2]}/{asset.Hash}"),
                    asset.Hash,
                    asset.Size)))
            .ToList();
        return new(inspection.Index.Id, artifacts, []);
    }
}
