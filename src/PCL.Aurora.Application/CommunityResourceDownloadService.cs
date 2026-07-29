using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class CommunityResourceDownloadService(IMinecraftDownloadExecutor downloadExecutor)
    : ICommunityResourceDownloadService
{
    public async Task<string> DownloadAsync(
        CommunityResourceProject project,
        CommunityResourceVersion version,
        string destinationDirectory,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DownloadWithDependenciesAsync(
            project,
            version,
            [],
            destinationDirectory,
            progress,
            cancellationToken).ConfigureAwait(false);
        return result.Paths[0];
    }

    public async Task<CommunityResourceDownloadResult> DownloadWithDependenciesAsync(
        CommunityResourceProject project,
        CommunityResourceVersion version,
        IReadOnlyList<CommunityResourceVersion> dependencies,
        string destinationDirectory,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(dependencies);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException("下载目录不能为空。", nameof(destinationDirectory));
        }

        var rootDirectory = Path.GetFullPath(destinationDirectory);
        var versions = new[] { version }
            .Concat(dependencies)
            .DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var artifacts = BuildArtifacts(project, versions);
        await downloadExecutor.ExecuteAsync(
            new MinecraftDownloadPlan(
                version.Id,
                artifacts,
                []),
            rootDirectory,
            progress,
            cancellationToken).ConfigureAwait(false);

        return new(
            artifacts.Select(artifact => Path.Combine(rootDirectory, artifact.RelativePath)).ToArray(),
            Math.Max(0, artifacts.Count - 1));
    }

    private static IReadOnlyList<MinecraftDownloadArtifact> BuildArtifacts(
        CommunityResourceProject project,
        IReadOnlyList<CommunityResourceVersion> versions)
    {
        var artifacts = new List<MinecraftDownloadArtifact>();
        var destinations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var version in versions)
        {
            var file = version.PrimaryFile ?? throw new InvalidOperationException($"{version.Name} 没有可下载文件。");
            var fileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(fileName, file.FileName, StringComparison.Ordinal) ||
                fileName is "." or "..")
            {
                throw new InvalidDataException("社区资源文件名不安全，已停止下载。");
            }

            if (destinations.TryGetValue(fileName, out var sha1))
            {
                if (string.Equals(sha1, file.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                throw new InvalidDataException($"{fileName} 与另一项依赖使用相同文件名，已停止下载。");
            }

            destinations.Add(fileName, file.Sha1);
            artifacts.Add(new(
                version.Id == versions[0].Id ? project.DisplayTitle : version.Name,
                fileName,
                file.Url,
                file.Sha1,
                file.Size));
        }

        return artifacts;
    }
}
