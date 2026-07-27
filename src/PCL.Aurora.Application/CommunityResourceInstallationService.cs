using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class CommunityResourceInstallationService(
    ICommunityResourceVersionService versionService,
    IMinecraftDownloadExecutor downloadExecutor) : ICommunityResourceInstallationService
{
    private const int MaximumResolvedVersions = 64;

    public async Task<CommunityResourceInstallationResult> InstallAsync(
        CommunityResourceProject project,
        CommunityResourceVersion version,
        MinecraftInstance instance,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(instance);

        var targetDirectory = GetTargetDirectory(project.Type);
        if (instance.Status != MinecraftInstanceStatus.Valid || string.IsNullOrWhiteSpace(instance.DirectoryPath))
        {
            throw new InvalidOperationException("所选实例不可用，不能安装社区资源。");
        }

        var gameVersion = instance.BaseVersionId ?? instance.VersionId;
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            throw new InvalidOperationException("无法确定所选实例的 Minecraft 版本。");
        }

        var loader = GetInstanceLoader(instance);
        EnsureCompatible(version, gameVersion, loader, project.Type);

        var resolvedVersions = new List<CommunityResourceVersion> { version };
        if (project.Type == CommunityResourceType.Mod)
        {
            await ResolveRequiredDependenciesAsync(
                version,
                gameVersion,
                loader,
                resolvedVersions,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { version.Id },
                cancellationToken).ConfigureAwait(false);
        }
        else if (version.Dependencies.Any(item => item.Type == CommunityResourceDependencyType.Required))
        {
            throw new InvalidOperationException("该资源包含必要依赖，请先在项目页确认后再安装。当前仅自动解析模组依赖。");
        }

        var artifacts = BuildArtifacts(resolvedVersions, targetDirectory);
        await downloadExecutor.ExecuteAsync(
            new MinecraftDownloadPlan(version.Id, artifacts, []),
            GetMinecraftRootDirectory(instance),
            progress,
            cancellationToken).ConfigureAwait(false);

        return new(
            artifacts.Count,
            Math.Max(0, resolvedVersions.Count - 1),
            artifacts.Select(artifact => Path.GetFileName(artifact.RelativePath)).ToArray());
    }

    private async Task ResolveRequiredDependenciesAsync(
        CommunityResourceVersion parent,
        string gameVersion,
        CommunityResourceLoader loader,
        List<CommunityResourceVersion> resolved,
        HashSet<string> seenVersionIds,
        CancellationToken cancellationToken)
    {
        foreach (var dependency in parent.Dependencies.Where(item => item.Type == CommunityResourceDependencyType.Required))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var catalog = !string.IsNullOrWhiteSpace(dependency.VersionId)
                ? await versionService.GetVersionAsync(dependency.VersionId, cancellationToken).ConfigureAwait(false)
                : await versionService.GetProjectVersionsAsync(
                    dependency.ProjectId!,
                    gameVersion,
                    loader,
                    cancellationToken).ConfigureAwait(false);
            var dependencyVersion = catalog.Versions.FirstOrDefault(candidate =>
                IsCompatible(candidate, gameVersion, loader));
            if (dependencyVersion is null)
            {
                var dependencyName = dependency.ProjectId ?? dependency.VersionId ?? dependency.FileName ?? "未知依赖";
                var detail = catalog.Errors.Count == 0 ? "没有兼容版本" : string.Join("；", catalog.Errors);
                throw new InvalidOperationException($"无法解析必要依赖 {dependencyName}：{detail}。");
            }

            if (!seenVersionIds.Add(dependencyVersion.Id))
            {
                continue;
            }

            if (resolved.Count >= MaximumResolvedVersions)
            {
                throw new InvalidOperationException($"必要依赖超过 {MaximumResolvedVersions - 1} 项，已停止安装。");
            }

            resolved.Add(dependencyVersion);
            await ResolveRequiredDependenciesAsync(
                dependencyVersion,
                gameVersion,
                loader,
                resolved,
                seenVersionIds,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<MinecraftDownloadArtifact> BuildArtifacts(
        IReadOnlyList<CommunityResourceVersion> versions,
        string targetDirectory)
    {
        var artifacts = new List<MinecraftDownloadArtifact>();
        var destinations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var version in versions)
        {
            var file = version.PrimaryFile ?? throw new InvalidOperationException($"{version.Name} 没有可下载文件。");
            if (destinations.TryGetValue(file.FileName, out var existingSha1))
            {
                if (string.Equals(existingSha1, file.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                throw new InvalidOperationException($"{file.FileName} 与另一项依赖使用相同文件名，已停止安装。");
            }

            destinations.Add(file.FileName, file.Sha1);
            artifacts.Add(new(
                version.Name,
                $"{targetDirectory}/{file.FileName}",
                file.Url,
                file.Sha1,
                file.Size));
        }

        return artifacts;
    }

    private static void EnsureCompatible(
        CommunityResourceVersion version,
        string gameVersion,
        CommunityResourceLoader loader,
        CommunityResourceType type)
    {
        if (!IsCompatible(version, gameVersion, loader))
        {
            throw new InvalidOperationException($"所选文件不兼容 Minecraft {gameVersion} 或当前加载器。");
        }

        if (type == CommunityResourceType.Mod && loader == CommunityResourceLoader.Any &&
            version.Loaders.Any(IsModLoader))
        {
            throw new InvalidOperationException("所选实例没有可识别的模组加载器。");
        }
    }

    private static bool IsCompatible(
        CommunityResourceVersion version,
        string gameVersion,
        CommunityResourceLoader loader) =>
        (version.GameVersions.Count == 0 || version.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase)) &&
        (loader == CommunityResourceLoader.Any ||
         !version.Loaders.Any(IsModLoader) ||
         version.Loaders.Contains(GetLoaderValue(loader), StringComparer.OrdinalIgnoreCase));

    private static bool IsModLoader(string value) => value.ToLowerInvariant() is
        "forge" or "neoforge" or "fabric" or "quilt";

    private static string GetTargetDirectory(CommunityResourceType type) => type switch
    {
        CommunityResourceType.Mod => "mods",
        CommunityResourceType.ResourcePack => "resourcepacks",
        CommunityResourceType.Shader => "shaderpacks",
        CommunityResourceType.DataPack => throw new InvalidOperationException("数据包需要先选择存档世界，当前不能直接安装到实例。"),
        CommunityResourceType.ModPack => throw new InvalidOperationException("整合包需要创建或导入独立实例，当前不能作为普通文件安装。"),
        _ => throw new InvalidOperationException("当前资源类型还没有可用的安装流程。"),
    };

    private static string GetMinecraftRootDirectory(MinecraftInstance instance)
    {
        var versionsDirectory = Directory.GetParent(instance.DirectoryPath)?.FullName;
        var rootDirectory = versionsDirectory is null ? null : Directory.GetParent(versionsDirectory)?.FullName;
        return string.IsNullOrWhiteSpace(rootDirectory)
            ? throw new InvalidOperationException("无法确定所选实例的 Minecraft 游戏目录。")
            : rootDirectory;
    }

    private static CommunityResourceLoader GetInstanceLoader(MinecraftInstance instance) =>
        instance.InstalledLoader?.Kind switch
        {
            MinecraftLoaderKind.Forge => CommunityResourceLoader.Forge,
            MinecraftLoaderKind.NeoForge => CommunityResourceLoader.NeoForge,
            MinecraftLoaderKind.Fabric => CommunityResourceLoader.Fabric,
            _ => CommunityResourceLoader.Any,
        };

    private static string GetLoaderValue(CommunityResourceLoader loader) => loader switch
    {
        CommunityResourceLoader.Forge => "forge",
        CommunityResourceLoader.NeoForge => "neoforge",
        CommunityResourceLoader.Fabric => "fabric",
        CommunityResourceLoader.Quilt => "quilt",
        _ => string.Empty,
    };
}
