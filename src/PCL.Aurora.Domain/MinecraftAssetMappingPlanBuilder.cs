namespace PCL.Aurora.Domain;

public static class MinecraftAssetMappingPlanBuilder
{
    public static MinecraftAssetMappingPlan Build(
        MinecraftAssetIndexParseResult inspection,
        string? minecraftRootDirectory,
        string? instanceDirectory)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (!inspection.IsSuccess || inspection.Index is null)
        {
            return new(null, [], [], inspection.Errors.Count > 0 ? inspection.Errors : ["资源索引无效。"]);
        }

        if (!inspection.Index.IsVirtual && !inspection.Index.MapsToResources)
        {
            return new(null, [], [], []);
        }

        if (string.IsNullOrWhiteSpace(minecraftRootDirectory) || string.IsNullOrWhiteSpace(instanceDirectory))
        {
            return new(null, [], [], ["无法确定资源映射目录。"]);
        }

        var rootDirectory = Path.GetFullPath(minecraftRootDirectory);
        var resolvedInstanceDirectory = Path.GetFullPath(instanceDirectory);
        if (!IsWithinDirectory(resolvedInstanceDirectory, rootDirectory))
        {
            return new(null, [], [], ["实例目录不能位于 Minecraft 根目录外。"]);
        }

        var assetsDirectory = Path.Combine(rootDirectory, "assets");
        var targetDirectory = inspection.Index.MapsToResources
            ? Path.Combine(resolvedInstanceDirectory, "resources")
            : Path.Combine(assetsDirectory, "virtual", inspection.Index.Id);
        var allowedTargetRoot = inspection.Index.MapsToResources ? resolvedInstanceDirectory : assetsDirectory;
        if (!IsWithinDirectory(Path.GetFullPath(targetDirectory), allowedTargetRoot))
        {
            return new(null, [], [], ["资源映射目录无效。"]);
        }

        var entries = new List<MinecraftAssetMappingEntry>();
        var missingFiles = new List<string>();
        foreach (var asset in inspection.Index.Objects)
        {
            var sourcePath = Path.Combine(assetsDirectory, "objects", asset.Hash[..2], asset.Hash);
            var destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, asset.Name.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinDirectory(destinationPath, targetDirectory))
            {
                return new(targetDirectory, [], [], [$"资源对象 {asset.Name} 的映射路径无效。"]);
            }

            if (!File.Exists(sourcePath))
            {
                missingFiles.Add(sourcePath);
                continue;
            }

            entries.Add(new(asset, sourcePath, destinationPath));
        }

        return new(targetDirectory, entries, missingFiles, []);
    }

    private static bool IsWithinDirectory(string path, string directory)
    {
        var fullDirectory = Path.GetFullPath(directory);
        var prefix = fullDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? fullDirectory
            : fullDirectory + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.Ordinal);
    }
}
