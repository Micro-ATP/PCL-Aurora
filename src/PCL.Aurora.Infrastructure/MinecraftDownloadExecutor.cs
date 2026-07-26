using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Infrastructure;

/// <summary>
/// 安全下载 Minecraft 构件。
///
/// 分片启用条件、Range 响应验证和失败时回退到单连接的行为直接适配自
/// PCL2 的 Plain Craft Launcher 2/Modules/Base/ModNet.vb，以及 PCL-CE 的
/// Plain Craft Launcher 2/Modules/Network/Downloader/FileDownloader.cs。
/// 本实现改用 .NET 跨平台 HTTP API，并保持 Aurora 的哈希校验与原子替换边界。
/// </summary>
public sealed class MinecraftDownloadExecutor(
    HttpClient httpClient,
    ILauncherPreferencesService? preferencesService = null) : IMinecraftDownloadExecutor
{
    private const long MinimumSizeForParallelRanges = 1024 * 1024;
    private const int ReadBufferSize = 81920;

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

        var preferences = preferencesService?.Current ?? LauncherPreferences.Default;
        var concurrency = preferences.DownloadConcurrency;
        var bandwidthLimiter = new DownloadBandwidthLimiter(
            LauncherDownloadSettings.GetSpeedLimitBytesPerSecond(preferences.DownloadSpeedLimitStep));
        using var requestSlots = new SemaphoreSlim(concurrency, concurrency);
        var rootDirectory = Path.GetFullPath(minecraftRootDirectory);
        await Parallel.ForEachAsync(
            artifacts,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = concurrency,
                CancellationToken = cancellationToken,
            },
            async (artifact, token) =>
            {
                var destinationPath = GetDestinationPath(rootDirectory, artifact.RelativePath);
                await DownloadArtifactAsync(artifact, destinationPath, requestSlots, concurrency, bandwidthLimiter, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    private async Task DownloadArtifactAsync(
        MinecraftDownloadArtifact artifact,
        string destinationPath,
        SemaphoreSlim requestSlots,
        int concurrency,
        DownloadBandwidthLimiter bandwidthLimiter,
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
                await DownloadFromSourceAsync(
                    artifact,
                    source,
                    destinationPath,
                    requestSlots,
                    concurrency,
                    bandwidthLimiter,
                    cancellationToken).ConfigureAwait(false);
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
        SemaphoreSlim requestSlots,
        int concurrency,
        DownloadBandwidthLimiter bandwidthLimiter,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new IOException("无法确定下载目标目录。");
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.partial");

        try
        {
            var result = await TryDownloadParallelRangesAsync(
                artifact,
                source,
                temporaryPath,
                requestSlots,
                concurrency,
                bandwidthLimiter,
                cancellationToken).ConfigureAwait(false);
            result ??= await DownloadSingleConnectionAsync(
                source,
                temporaryPath,
                requestSlots,
                bandwidthLimiter,
                cancellationToken).ConfigureAwait(false);

            VerifyArtifact(artifact, result.Value.DownloadedSize, result.Value.Sha1);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private async Task<DownloadResult?> TryDownloadParallelRangesAsync(
        MinecraftDownloadArtifact artifact,
        Uri source,
        string temporaryPath,
        SemaphoreSlim requestSlots,
        int concurrency,
        DownloadBandwidthLimiter bandwidthLimiter,
        CancellationToken cancellationToken)
    {
        if (artifact.Size is not { } size ||
            size < MinimumSizeForParallelRanges ||
            concurrency <= 1 ||
            !IsSha1(artifact.Sha1))
        {
            return null;
        }

        if (!await SupportsParallelRangesAsync(source, size, requestSlots, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var chunkCount = Math.Min(concurrency, (int)Math.Ceiling(size / (double)MinimumSizeForParallelRanges));
        var chunkSize = (long)Math.Ceiling(size / (double)chunkCount);
        var ranges = Enumerable.Range(0, chunkCount)
            .Select(index =>
            {
                var start = index * chunkSize;
                return new ByteRange(start, Math.Min(size - 1, start + chunkSize - 1));
            })
            .Where(range => range.Start <= range.End)
            .ToArray();

        try
        {
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             ReadBufferSize,
                             useAsync: true))
            {
                destination.SetLength(size);
                var handle = destination.SafeFileHandle;
                await Parallel.ForEachAsync(
                    ranges,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = chunkCount,
                        CancellationToken = cancellationToken,
                    },
                    async (range, token) =>
                    {
                        await DownloadRangeAsync(
                            source,
                            range,
                            size,
                            handle,
                            requestSlots,
                            bandwidthLimiter,
                            token).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var content = new FileStream(
                temporaryPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                ReadBufferSize,
                useAsync: true);
            return new DownloadResult(size, await SHA1.HashDataAsync(content, cancellationToken).ConfigureAwait(false));
        }
        catch (RangeNotSupportedException)
        {
            TryDelete(temporaryPath);
            return null;
        }
    }

    private async Task<bool> SupportsParallelRangesAsync(
        Uri source,
        long expectedSize,
        SemaphoreSlim requestSlots,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source);
        request.Headers.Range = new RangeHeaderValue(0, 0);
        await requestSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            var contentRange = response.Content.Headers.ContentRange;
            return response.StatusCode == HttpStatusCode.PartialContent &&
                   response.Headers.AcceptRanges.Any(value => string.Equals(value, "bytes", StringComparison.OrdinalIgnoreCase)) &&
                   contentRange is { From: 0, To: 0, Length: not null } &&
                   contentRange.Length == expectedSize;
        }
        finally
        {
            requestSlots.Release();
        }
    }

    private async Task DownloadRangeAsync(
        Uri source,
        ByteRange range,
        long expectedSize,
        SafeFileHandle destinationHandle,
        SemaphoreSlim requestSlots,
        DownloadBandwidthLimiter bandwidthLimiter,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source);
        request.Headers.Range = new RangeHeaderValue(range.Start, range.End);
        await requestSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.PartialContent ||
                !IsExpectedContentRange(response.Content.Headers.ContentRange, range, expectedSize))
            {
                throw new RangeNotSupportedException();
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var offset = range.Start;
            var remaining = range.Length;
            var buffer = new byte[ReadBufferSize];
            while (remaining > 0)
            {
                var bytesRead = await content.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new InvalidDataException("HTTP Range 响应在范围结束前中断。");
                }

                await bandwidthLimiter.WaitAsync(bytesRead, cancellationToken).ConfigureAwait(false);
                await RandomAccess.WriteAsync(destinationHandle, buffer.AsMemory(0, bytesRead), offset, cancellationToken).ConfigureAwait(false);
                offset += bytesRead;
                remaining -= bytesRead;
            }

            if (await content.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) != 0)
            {
                throw new InvalidDataException("HTTP Range 响应超过声明的范围。");
            }
        }
        finally
        {
            requestSlots.Release();
        }
    }

    private async Task<DownloadResult> DownloadSingleConnectionAsync(
        Uri source,
        string temporaryPath,
        SemaphoreSlim requestSlots,
        DownloadBandwidthLimiter bandwidthLimiter,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source);
        await requestSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await httpClient.SendAsync(
                request,
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
                             ReadBufferSize,
                             useAsync: true))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
                var buffer = new byte[ReadBufferSize];
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await bandwidthLimiter.WaitAsync(bytesRead, cancellationToken).ConfigureAwait(false);
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    hash.AppendData(buffer, 0, bytesRead);
                    downloadedSize += bytesRead;
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                sha1 = hash.GetHashAndReset();
            }

            return new DownloadResult(downloadedSize, sha1);
        }
        finally
        {
            requestSlots.Release();
        }
    }

    private static bool IsExpectedContentRange(ContentRangeHeaderValue? contentRange, ByteRange range, long expectedSize) =>
        contentRange is { From: not null, To: not null, Length: not null } &&
        contentRange.From == range.Start &&
        contentRange.To == range.End &&
        contentRange.Length == expectedSize;

    private static bool IsSha1(string? value) =>
        value is { Length: 40 } && value.All(Uri.IsHexDigit);

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

    private readonly record struct ByteRange(long Start, long End)
    {
        public long Length => End - Start + 1;
    }

    private readonly record struct DownloadResult(long DownloadedSize, byte[] Sha1);

    private sealed class RangeNotSupportedException : IOException;

    /// <summary>
    /// 单次安装共用的限速队列；时间预留使并行请求也不会叠加突破用户上限。
    /// </summary>
    private sealed class DownloadBandwidthLimiter(long? bytesPerSecond)
    {
        private readonly object syncRoot = new();
        private DateTimeOffset nextAvailableAt = DateTimeOffset.MinValue;

        public async Task WaitAsync(int byteCount, CancellationToken cancellationToken)
        {
            if (bytesPerSecond is not { } limit || byteCount <= 0)
            {
                return;
            }

            TimeSpan delay;
            lock (syncRoot)
            {
                var now = DateTimeOffset.UtcNow;
                var start = nextAvailableAt > now ? nextAvailableAt : now;
                nextAvailableAt = start + TimeSpan.FromSeconds(byteCount / (double)limit);
                delay = start - now;
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
