using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftLoaderPackageDownloadService(IMinecraftDownloadExecutor downloadExecutor)
    : IMinecraftLoaderPackageDownloadService
{
    public async Task<string> DownloadAsync(
        MinecraftLoaderPackageEntry package,
        string destinationFile,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (string.IsNullOrWhiteSpace(destinationFile))
        {
            throw new ArgumentException("保存位置不能为空。", nameof(destinationFile));
        }

        var fullPath = Path.GetFullPath(destinationFile);
        var destinationDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new IOException("无法确定安装包保存目录。");
        var fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
        {
            throw new InvalidDataException("安装包文件名无效。");
        }

        await downloadExecutor.ExecuteAsync(
            new MinecraftDownloadPlan(
                $"{package.Kind}-{package.Version}",
                [new(
                    $"{package.Kind} {package.Version}",
                    fileName,
                    package.DownloadUri,
                    Sha1: null,
                    Size: null,
                    AlternativeUrls: package.AlternativeUris,
                    MinimumSize: package.MinimumSize)],
                []),
            destinationDirectory,
            progress,
            cancellationToken).ConfigureAwait(false);
        return fullPath;
    }
}
