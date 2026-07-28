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
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(version);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException("下载目录不能为空。", nameof(destinationDirectory));
        }

        var file = version.PrimaryFile ?? throw new InvalidOperationException("所选版本没有可下载文件。");
        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, file.FileName, StringComparison.Ordinal) ||
            fileName is "." or "..")
        {
            throw new InvalidDataException("社区资源文件名不安全，已停止下载。");
        }

        var rootDirectory = Path.GetFullPath(destinationDirectory);
        await downloadExecutor.ExecuteAsync(
            new MinecraftDownloadPlan(
                version.Id,
                [new MinecraftDownloadArtifact(project.DisplayTitle, fileName, file.Url, file.Sha1, file.Size)],
                []),
            rootDirectory,
            progress,
            cancellationToken).ConfigureAwait(false);

        return Path.Combine(rootDirectory, fileName);
    }
}
