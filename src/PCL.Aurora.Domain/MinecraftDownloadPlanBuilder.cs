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
            artifacts.Add(Pcl2VerifiedMirrorSourceMapper.PreferMirrorWhenVerified(
                new("Minecraft 客户端", $"versions/{metadata.Id}/{metadata.Id}.jar", client.Url, client.Sha1, client.Size)));
        }
        else
        {
            blockingReasons.Add("版本元数据未提供 Minecraft 客户端下载信息。");
        }

        if (metadata.AssetIndex is { } assetIndex)
        {
            artifacts.Add(Pcl2VerifiedMirrorSourceMapper.PreferMirrorWhenVerified(
                new("Minecraft 资源索引", $"assets/indexes/{assetIndex.Id}.json", assetIndex.Url, assetIndex.Sha1, assetIndex.Size)));
        }
        else
        {
            blockingReasons.Add("版本元数据未提供资源索引下载信息。");
        }

        return new(metadata.Id, artifacts, blockingReasons);
    }

    public static MinecraftDownloadPlan Create(
        MinecraftVersionMetadataInspection inspection,
        JavaArchitecture architecture,
        MinecraftLaunchRuleEnvironment? ruleEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        var basePlan = Create(inspection.EffectiveMetadata);
        if (inspection.EffectiveMetadata is null)
        {
            return basePlan;
        }

        var artifacts = basePlan.Artifacts.ToList();
        var blockingReasons = basePlan.BlockingReasons.ToList();
        foreach (var library in inspection.EffectiveMetadata.Libraries ?? [])
        {
            if (!ShouldIncludeLibrary(library, ruleEnvironment, blockingReasons))
            {
                continue;
            }

            AddArtifact(
                artifacts,
                blockingReasons,
                $"支持库 {library.Name}",
                library.ArtifactPath,
                library.Artifact);

            if (!MinecraftNativeLibraryPlanBuilder.TrySelectMacOSClassifier(
                    library,
                    architecture,
                    out var classifier,
                    out var nativeSelectionReason))
            {
                if (nativeSelectionReason is not null)
                {
                    blockingReasons.Add($"原生库 {library.Name}：{nativeSelectionReason}");
                }

                continue;
            }

            if (library.Classifiers is null ||
                !library.Classifiers.TryGetValue(classifier!, out var classifierInfo))
            {
                blockingReasons.Add($"原生库 {library.Name} 未提供 classifier {classifier} 的下载描述。");
                continue;
            }

            AddArtifact(
                artifacts,
                blockingReasons,
                $"原生库 {library.Name} ({classifier})",
                classifierInfo.Path,
                classifierInfo.Download);
        }

        return new(
            basePlan.VersionId,
            artifacts.DistinctBy(artifact => artifact.RelativePath, StringComparer.Ordinal).ToList(),
            blockingReasons.Distinct(StringComparer.Ordinal).ToList());
    }

    internal static bool ShouldIncludeLibrary(
        MinecraftVersionLibrary library,
        MinecraftLaunchRuleEnvironment? ruleEnvironment,
        List<string> blockingReasons)
    {
        if (library.HasUnsupportedRules || (library.HasConditionalRules && library.Rules is null))
        {
            blockingReasons.Add($"库 {library.Name} 包含无法安全解析的条件规则。");
            return false;
        }

        if (!library.HasConditionalRules)
        {
            return true;
        }

        if (ruleEnvironment is null)
        {
            blockingReasons.Add($"库 {library.Name} 包含条件规则，但未提供规则执行环境。");
            return false;
        }

        return PclCeMinecraftLaunchRuleEvaluator.IsAllowed(library.Rules, ruleEnvironment);
    }

    private static void AddArtifact(
        List<MinecraftDownloadArtifact> artifacts,
        List<string> blockingReasons,
        string description,
        string? path,
        MinecraftVersionDownload? download)
    {
        if (path is null && download is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(path) || download is null)
        {
            blockingReasons.Add($"{description} 缺少完整下载描述。");
            return;
        }

        string relativePath;
        try
        {
            relativePath = GetLibraryRelativePath(path);
        }
        catch (InvalidDataException exception)
        {
            blockingReasons.Add($"{description} 的路径无效：{exception.Message}");
            return;
        }

        artifacts.Add(Pcl2VerifiedMirrorSourceMapper.PreferMirrorWhenVerified(
            new(description, relativePath, download.Url, download.Sha1, download.Size)));
    }

    private static string GetLibraryRelativePath(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        if (Path.IsPathRooted(normalizedPath) ||
            normalizedPath.StartsWith("/", StringComparison.Ordinal) ||
            normalizedPath.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException("artifact 路径必须位于 libraries 目录内。");
        }

        return $"libraries/{normalizedPath}";
    }
}
