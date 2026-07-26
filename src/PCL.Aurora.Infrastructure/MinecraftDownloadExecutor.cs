using System.Security.Cryptography;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Infrastructure;

public sealed class MinecraftDownloadExecutor(HttpClient httpClient) : IMinecraftDownloadExecutor
{
    private const int MaximumConcurrentArtifacts = 4;

    public async Task ExecuteAsync(
        MinecraftDownloadPlan downloadPlan,
        string minecraftRootDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(downloadPlan);
        await ExecuteArtifactsAsync(downloadPlan.IsReady, downloadPlan.Artifacts, minecraftRootDirectory, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExecuteAsync(
        MinecraftAssetDownloadPlan downloadPlan,
        string minecraftRootDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(downloadPlan);
        await ExecuteArtifactsAsync(downloadPlan.IsReady, downloadPlan.Artifacts, minecraftRootDirectory, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteArtifactsAsync(
        bool isReady,
        IReadOnlyList<MinecraftDownloadArtifact> artifacts,
        string minecraftRootDirectory,
        CancellationToken cancellationToken)
    {
        if (!isReady)
        {
            throw new InvalidOperationException("下载计划不完整，不能执行。");
        }

        if (string.IsNullOrWhiteSpace(minecraftRootDirectory))
        {
            throw new ArgumentException("Minecraft 根目录不能为空。", nameof(minecraftRootDirectory));
        }

        var rootDirectory = Path.GetFullPath(minecraftRootDirectory);
        await Parallel.ForEachAsync(
            artifacts,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaximumConcurrentArtifacts,
                CancellationToken = cancellationToken,
            },
            async (artifact, token) =>
            {
                var destinationPath = GetDestinationPath(rootDirectory, artifact.RelativePath);
                await DownloadArtifactAsync(artifact, destinationPath, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    private async Task DownloadArtifactAsync(
        MinecraftDownloadArtifact artifact,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var sources = new[] { artifact.Url }
            .Concat(artifact.AlternativeUrls ?? [])
            .Distinct()
            .ToArray();
        var failures = new List<string>();
        Exception? lastFailure = null;
        foreach (var source in sources)
        {
            try
            {
                await DownloadFromSourceAsync(artifact, source, destinationPath, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException)
            {
                lastFailure = exception;
                failures.Add($"{source.Host}：{exception.Message}");
            }
        }

        if (sources.Length == 1 && lastFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(lastFailure).Throw();
        }

        throw new IOException($"{artifact.Description} 的全部下载源均失败：{string.Join("；", failures)}");
    }

    private async Task DownloadFromSourceAsync(
        MinecraftDownloadArtifact artifact,
        Uri source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new IOException("无法确定下载目标目录。");
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.partial");

        try
        {
            using var response = await httpClient.GetAsync(
                source,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            long downloadedSize = 0;
            byte[] sha1;
            await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
                var buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    hash.AppendData(buffer, 0, bytesRead);
                    downloadedSize += bytesRead;
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                sha1 = hash.GetHashAndReset();
            }

            VerifyArtifact(artifact, downloadedSize, sha1);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static string GetDestinationPath(string rootDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("下载目标必须是相对于 Minecraft 根目录的路径。");
        }

        var destinationPath = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));
        var rootPrefix = rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? rootDirectory
            : rootDirectory + Path.DirectorySeparatorChar;
        if (!destinationPath.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("下载目标超出 Minecraft 根目录。");
        }

        return destinationPath;
    }

    private static void VerifyArtifact(MinecraftDownloadArtifact artifact, long downloadedSize, byte[] sha1)
    {
        if (artifact.Size is { } expectedSize && downloadedSize != expectedSize)
        {
            throw new InvalidDataException($"{artifact.Description} 的文件大小校验失败。");
        }

        if (string.IsNullOrWhiteSpace(artifact.Sha1))
        {
            return;
        }

        var actualSha1 = Convert.ToHexString(sha1);
        if (!string.Equals(actualSha1, artifact.Sha1, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{artifact.Description} 的 SHA-1 校验失败。");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
