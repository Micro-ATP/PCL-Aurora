namespace PCL.Aurora.Domain;

public static class MinecraftVersionMetadataResolver
{
    public static MinecraftVersionMetadataInspection Resolve(IReadOnlyList<MinecraftVersionMetadata> inheritanceChain)
    {
        if (inheritanceChain.Count == 0)
        {
            return new([], null, ["未读取到版本元数据。"]);
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var metadata in inheritanceChain)
        {
            if (!seenIds.Add(metadata.Id))
            {
                return new(inheritanceChain, null, [$"检测到版本继承循环：{metadata.Id}。"]);
            }
        }

        var effective = inheritanceChain[^1];
        for (var index = inheritanceChain.Count - 2; index >= 0; index--)
        {
            var child = inheritanceChain[index];
            effective = effective with
            {
                Id = child.Id,
                InheritsFrom = child.InheritsFrom,
                Type = child.Type ?? effective.Type,
                ReleaseTime = child.ReleaseTime ?? effective.ReleaseTime,
                ClientDownload = child.ClientDownload ?? effective.ClientDownload,
                AssetIndex = child.AssetIndex ?? effective.AssetIndex,
            };
        }

        return new(inheritanceChain, effective, []);
    }
}
