namespace PCL.Aurora.Domain;

public static class MinecraftNativeLibraryPlanBuilder
{
    public static MinecraftNativeLibraryPlan Build(
        MinecraftVersionMetadataInspection inspection,
        string? minecraftRootDirectory,
        string? nativesDirectory,
        JavaArchitecture architecture,
        MinecraftLaunchRuleEnvironment? ruleEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        var archives = new List<MinecraftNativeLibraryArchive>();
        var missingFiles = new List<string>();
        var blockingReasons = new List<string>();
        if (inspection.EffectiveMetadata is null)
        {
            return new(string.Empty, [], [], ["未读取到有效版本元数据。"]);
        }

        if (string.IsNullOrWhiteSpace(minecraftRootDirectory))
        {
            return new(string.Empty, [], [], ["无法确定 Minecraft 根目录。"]);
        }

        if (string.IsNullOrWhiteSpace(nativesDirectory))
        {
            return new(string.Empty, [], [], ["无法确定 native 目录。"]);
        }

        var rootDirectory = Path.GetFullPath(minecraftRootDirectory);
        var librariesDirectory = Path.Combine(rootDirectory, "libraries");
        var resolvedNativesDirectory = Path.GetFullPath(nativesDirectory);
        if (!IsWithinDirectory(resolvedNativesDirectory, rootDirectory))
        {
            return new(resolvedNativesDirectory, [], [], ["native 目录不能位于 Minecraft 根目录外。"]);
        }

        foreach (var library in inspection.EffectiveMetadata.Libraries ?? [])
        {
            if (!TrySelectMacOSClassifier(library, architecture, out var classifier, out var reason))
            {
                if (reason is not null)
                {
                    blockingReasons.Add($"原生库 {library.Name}：{reason}");
                }

                continue;
            }

            if (!MinecraftDownloadPlanBuilder.ShouldIncludeLibrary(library, ruleEnvironment, blockingReasons))
            {
                continue;
            }

            if (library.Classifiers is null ||
                !library.Classifiers.TryGetValue(classifier!, out var classifierInfo) ||
                classifierInfo.Download is null)
            {
                blockingReasons.Add($"原生库 {library.Name} 未提供 classifier {classifier} 的下载描述。");
                continue;
            }

            var relativePath = classifierInfo.Path;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                blockingReasons.Add($"原生库 {library.Name} 未提供 classifier {classifier} 的本地路径。");
                continue;
            }

            string localPath;
            try
            {
                localPath = GetLibraryPath(librariesDirectory, relativePath);
            }
            catch (InvalidDataException exception)
            {
                blockingReasons.Add($"原生库 {library.Name} 的路径无效：{exception.Message}");
                continue;
            }

            if (File.Exists(localPath))
            {
                archives.Add(new(library.Name, classifier!, localPath, classifierInfo.Download));
            }
            else
            {
                missingFiles.Add(localPath);
            }
        }

        return new(
            resolvedNativesDirectory,
            archives.DistinctBy(archive => archive.LocalPath, StringComparer.Ordinal).ToList(),
            missingFiles.Distinct(StringComparer.Ordinal).ToList(),
            blockingReasons.Distinct(StringComparer.Ordinal).ToList());
    }

    public static bool TrySelectMacOSClassifier(
        MinecraftVersionLibrary library,
        JavaArchitecture architecture,
        out string? classifier,
        out string? reason)
    {
        classifier = null;
        reason = null;
        if (library.NativeClassifiers is null)
        {
            return false;
        }

        if (!library.NativeClassifiers.TryGetValue("osx", out var pattern) &&
            !library.NativeClassifiers.TryGetValue("macos", out pattern))
        {
            return false;
        }

        var architectureValue = architecture switch
        {
            JavaArchitecture.Arm64 => "arm64",
            JavaArchitecture.X64 => "x86_64",
            _ => null,
        };
        if (pattern.Contains("${arch}", StringComparison.Ordinal) && architectureValue is null)
        {
            reason = "无法为未知 Java 架构选择 classifier。";
            return false;
        }

        classifier = pattern.Replace("${arch}", architectureValue, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(classifier) || classifier.Contains("${", StringComparison.Ordinal))
        {
            reason = "classifier 包含未解析占位符。";
            classifier = null;
            return false;
        }

        return true;
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
        if (!IsWithinDirectory(localPath, rootDirectory))
        {
            throw new InvalidDataException("artifact 路径不能位于 libraries 目录外。");
        }

        return localPath;
    }

    private static bool IsWithinDirectory(string path, string directory)
    {
        var directoryPrefix = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;
        return path.StartsWith(directoryPrefix, StringComparison.Ordinal);
    }
}
