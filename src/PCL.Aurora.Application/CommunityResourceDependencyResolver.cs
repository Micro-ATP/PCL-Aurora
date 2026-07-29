// Directly adapts the dependency-selection flow from PCL-CE
// Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs and
// Plain Craft Launcher 2/Pages/PageDownload/Comp/PageDownloadCompDetail.xaml.cs.
// Modified by Micro-ATP to resolve Modrinth versions before any filesystem write.
// See LICENSES/PCL-CE-Plain-Craft-Launcher-2-LICENCE.txt and NOTICE.
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class CommunityResourceDependencyResolver(ICommunityResourceVersionService versionService)
    : ICommunityResourceDependencyResolver
{
    private const int MaximumResolvedVersions = 64;

    public async Task<CommunityResourceDependencyPreparation> ResolveAsync(
        CommunityResourceVersion version,
        string? gameVersion,
        CommunityResourceLoader loader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        var cache = new Dictionary<string, CommunityResourceVersion?>(StringComparer.OrdinalIgnoreCase);
        var required = new List<CommunityResourceVersion>();
        var optional = new List<CommunityResourceOptionalDependency>();
        var errors = new List<string>();
        var requiredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { version.Id };

        foreach (var dependency in version.Dependencies.Where(item => item.Type == CommunityResourceDependencyType.Required))
        {
            await AddRequiredDependencyAsync(
                dependency,
                gameVersion,
                loader,
                required,
                requiredIds,
                cache,
                errors,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var dependency in version.Dependencies.Where(item => item.Type == CommunityResourceDependencyType.Optional))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolved = await ResolveVersionAsync(
                dependency,
                gameVersion,
                loader,
                cache,
                cancellationToken).ConfigureAwait(false);
            if (resolved is null)
            {
                errors.Add($"可选依赖 {GetDependencyName(dependency)} 没有兼容文件。");
                continue;
            }

            if (requiredIds.Contains(resolved.Id))
            {
                continue;
            }

            var bundle = new List<CommunityResourceVersion> { resolved };
            var bundleIds = new HashSet<string>(requiredIds, StringComparer.OrdinalIgnoreCase) { resolved.Id };
            foreach (var transitive in resolved.Dependencies.Where(item => item.Type == CommunityResourceDependencyType.Required))
            {
                await AddRequiredDependencyAsync(
                    transitive,
                    gameVersion,
                    loader,
                    bundle,
                    bundleIds,
                    cache,
                    errors,
                    cancellationToken).ConfigureAwait(false);
            }

            optional.Add(new(
                resolved.Id,
                $"{resolved.Name}（{resolved.VersionNumber}）",
                bundle));
        }

        return new(required, optional, errors);
    }

    private async Task AddRequiredDependencyAsync(
        CommunityResourceDependency dependency,
        string? gameVersion,
        CommunityResourceLoader loader,
        List<CommunityResourceVersion> target,
        HashSet<string> seenIds,
        Dictionary<string, CommunityResourceVersion?> cache,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target.Count >= MaximumResolvedVersions)
        {
            throw new InvalidOperationException($"依赖数量超过 {MaximumResolvedVersions} 项，已停止解析。");
        }

        var resolved = await ResolveVersionAsync(
            dependency,
            gameVersion,
            loader,
            cache,
            cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            errors.Add($"必要依赖 {GetDependencyName(dependency)} 没有兼容文件。");
            return;
        }

        if (!seenIds.Add(resolved.Id))
        {
            return;
        }

        target.Add(resolved);
        foreach (var transitive in resolved.Dependencies.Where(item => item.Type == CommunityResourceDependencyType.Required))
        {
            await AddRequiredDependencyAsync(
                transitive,
                gameVersion,
                loader,
                target,
                seenIds,
                cache,
                errors,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<CommunityResourceVersion?> ResolveVersionAsync(
        CommunityResourceDependency dependency,
        string? gameVersion,
        CommunityResourceLoader loader,
        Dictionary<string, CommunityResourceVersion?> cache,
        CancellationToken cancellationToken)
    {
        var key = !string.IsNullOrWhiteSpace(dependency.VersionId)
            ? $"version:{dependency.VersionId}"
            : $"project:{dependency.ProjectId}:{gameVersion}:{loader}";
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var catalog = !string.IsNullOrWhiteSpace(dependency.VersionId)
            ? await versionService.GetVersionAsync(dependency.VersionId, cancellationToken).ConfigureAwait(false)
            : await versionService.GetProjectVersionsAsync(
                dependency.ProjectId!,
                gameVersion,
                loader,
                cancellationToken).ConfigureAwait(false);
        var resolved = catalog.Versions
            .Where(candidate => IsCompatible(candidate, gameVersion, loader))
            .OrderBy(candidate => candidate.Channel)
            .ThenByDescending(candidate => candidate.PublishedAt)
            .FirstOrDefault();
        cache[key] = resolved;
        return resolved;
    }

    private static bool IsCompatible(
        CommunityResourceVersion version,
        string? gameVersion,
        CommunityResourceLoader loader) =>
        (string.IsNullOrWhiteSpace(gameVersion) ||
         version.GameVersions.Count == 0 ||
         version.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase)) &&
        (loader == CommunityResourceLoader.Any ||
         !version.Loaders.Any(IsModLoader) ||
         version.Loaders.Contains(GetLoaderValue(loader), StringComparer.OrdinalIgnoreCase));

    private static bool IsModLoader(string value) => value.ToLowerInvariant() is
        "forge" or "neoforge" or "fabric" or "quilt";

    private static string GetLoaderValue(CommunityResourceLoader loader) => loader switch
    {
        CommunityResourceLoader.Forge => "forge",
        CommunityResourceLoader.NeoForge => "neoforge",
        CommunityResourceLoader.Fabric => "fabric",
        CommunityResourceLoader.Quilt => "quilt",
        _ => string.Empty,
    };

    private static string GetDependencyName(CommunityResourceDependency dependency) =>
        dependency.FileName ?? dependency.ProjectId ?? dependency.VersionId ?? "未知依赖";
}
