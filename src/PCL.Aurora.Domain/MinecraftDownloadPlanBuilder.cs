namespace PCL.Aurora.Domain;

public static class MinecraftDownloadPlanBuilder
{
    public static MinecraftDownloadPlan Create(MinecraftVersionMetadata? metadata)
    {
        if (metadata is null)
        {
            return new(null, [], ["没有可用于生成下载计划的版本元数据。"]);
        }

        var artifacts = new List<MinecraftDownloadArtifact>();
        var blockingReasons = new List<string>();
        if (metadata.ClientDownload is { } client)
        {
            artifacts.Add(new("Minecraft 客户端", $"versions/{metadata.Id}/{metadata.Id}.jar", client.Url, client.Sha1, client.Size));
        }
        else
        {
            blockingReasons.Add("版本元数据未提供 Minecraft 客户端下载信息。");
        }

        if (metadata.AssetIndex is { } assetIndex)
        {
            artifacts.Add(new("Minecraft 资源索引", $"assets/indexes/{assetIndex.Id}.json", assetIndex.Url, assetIndex.Sha1, assetIndex.Size));
        }
        else
        {
            blockingReasons.Add("版本元数据未提供资源索引下载信息。");
        }

        return new(metadata.Id, artifacts, blockingReasons);
    }
}
