namespace PCL.Aurora.Domain;

public static class MinecraftClasspathBuilder
{
    public static MinecraftClasspathInspection Build(
        MinecraftVersionMetadataInspection inspection,
        string? minecraftRootDirectory,
        MinecraftLaunchRuleEnvironment? ruleEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        var entries = new List<string>();
        var missingFiles = new List<string>();
        var blockingReasons = new List<string>();
        if (inspection.EffectiveMetadata is null)
        {
            return new([], [], ["未读取到有效版本元数据。"]);
        }

        if (string.IsNullOrWhiteSpace(minecraftRootDirectory))
        {
            return new([], [], ["无法确定 Minecraft 根目录。"]);
        }

        var rootDirectory = Path.GetFullPath(minecraftRootDirectory);
        var librariesDirectory = Path.Combine(rootDirectory, "libraries");
        var libraries = inspection.EffectiveMetadata.Libraries ?? [];
        if (libraries.Count == 0)
        {
            blockingReasons.Add("版本元数据未提供可解析的 libraries。");
        }

        foreach (var library in libraries)
        {
            if (!MinecraftDownloadPlanBuilder.ShouldIncludeLibrary(library, ruleEnvironment, blockingReasons))
            {
                continue;
            }

            if (library.ArtifactPath is null || library.Artifact is null)
            {
                blockingReasons.Add($"库 {library.Name} 未提供显式 artifact 下载描述。");
                continue;
            }

            string localPath;
            try
            {
                localPath = GetLibraryPath(librariesDirectory, library.ArtifactPath);
            }
            catch (InvalidDataException exception)
            {
                blockingReasons.Add($"库 {library.Name} 的路径无效：{exception.Message}");
                continue;
            }

            if (File.Exists(localPath))
            {
                entries.Add(localPath);
            }
            else
            {
                missingFiles.Add(localPath);
            }
        }

        var clientMetadata = inspection.InheritanceChain
            .Reverse()
            .FirstOrDefault(metadata => metadata.ClientDownload is not null);
        if (clientMetadata is null)
        {
            blockingReasons.Add("版本继承链未提供 Minecraft 客户端下载描述。");
        }
        else
        {
            var clientJarPath = Path.Combine(rootDirectory, "versions", clientMetadata.Id, $"{clientMetadata.Id}.jar");
            if (File.Exists(clientJarPath))
            {
                entries.Add(clientJarPath);
            }
            else
            {
                missingFiles.Add(clientJarPath);
            }
        }

        return new(entries.Distinct(StringComparer.Ordinal).ToList(), missingFiles, blockingReasons);
    }

    private static string GetLibraryPath(string librariesDirectory, string artifactPath)
    {
        var normalizedPath = artifactPath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedPath))
        {
            throw new InvalidDataException("artifact 路径不能是绝对路径。");
        }

        var rootDirectory = Path.GetFullPath(librariesDirectory);
        var localPath = Path.GetFullPath(Path.Combine(rootDirectory, normalizedPath));
        var rootPrefix = rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? rootDirectory
            : rootDirectory + Path.DirectorySeparatorChar;
        if (!localPath.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("artifact 路径不能位于 libraries 目录外。");
        }

        return localPath;
    }
}
