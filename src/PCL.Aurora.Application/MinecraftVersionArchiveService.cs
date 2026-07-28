using System.Security.Cryptography;
using System.Text.Json;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// Implements PCL's explicit client-core and server save actions with cross-platform paths.
/// Files become visible at the destination only after metadata and checksum validation succeed.
/// </summary>
public sealed class MinecraftVersionArchiveService(HttpClient httpClient) : IMinecraftVersionArchiveService
{
    public async Task<string> SaveClientCoreAsync(
        MinecraftVersionCatalogEntry version,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ValidateVersion(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var root = Path.GetFullPath(destinationDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException("所选保存目录不存在。");
        }

        var targetDirectory = Path.Combine(root, version.Id);
        if (Directory.Exists(targetDirectory) && Directory.EnumerateFileSystemEntries(targetDirectory).Any())
        {
            throw new IOException($"目标目录中已存在 {version.Id}，不会覆盖。");
        }

        var stagingDirectory = Path.Combine(root, $".{version.Id}.{Guid.NewGuid():N}.partial");
        Directory.CreateDirectory(stagingDirectory);
        var metadataTarget = Path.Combine(stagingDirectory, $"{version.Id}.json");
        var clientTarget = Path.Combine(stagingDirectory, $"{version.Id}.jar");
        try
        {
            using var metadata = await FetchMetadataAsync(version, cancellationToken).ConfigureAwait(false);
            var client = GetDownload(metadata.Document.RootElement, "client", "该版本未提供客户端核心下载。 ");
            await File.WriteAllTextAsync(metadataTarget, metadata.Json, cancellationToken).ConfigureAwait(false);
            await DownloadAndVerifyAsync(client, clientTarget, cancellationToken).ConfigureAwait(false);

            if (Directory.Exists(targetDirectory))
            {
                if (Directory.EnumerateFileSystemEntries(targetDirectory).Any())
                {
                    throw new IOException($"目标目录中已存在 {version.Id}，不会覆盖。");
                }

                Directory.Delete(targetDirectory);
            }

            Directory.Move(stagingDirectory, targetDirectory);
            return targetDirectory;
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    public async Task SaveServerAsync(
        MinecraftVersionCatalogEntry version,
        string destinationFile,
        CancellationToken cancellationToken = default)
    {
        ValidateVersion(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFile);

        var target = Path.GetFullPath(destinationFile);
        var parent = Path.GetDirectoryName(target);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
        {
            throw new DirectoryNotFoundException("所选保存目录不存在。");
        }

        var temporary = target + $".{Guid.NewGuid():N}.partial";
        try
        {
            using var metadata = await FetchMetadataAsync(version, cancellationToken).ConfigureAwait(false);
            var server = GetDownload(metadata.Document.RootElement, "server", "该版本未提供官方服务端下载。 ");
            await DownloadAndVerifyAsync(server, temporary, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private async Task<VersionMetadataDocument> FetchMetadataAsync(
        MinecraftVersionCatalogEntry version,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(version.MetadataUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("id", out var id) ||
            !string.Equals(id.GetString(), version.Id, StringComparison.Ordinal))
        {
            document.Dispose();
            throw new InvalidDataException("下载的版本元数据与所选版本不匹配。");
        }

        return new VersionMetadataDocument(json, document);
    }

    private static MinecraftVersionDownload GetDownload(JsonElement root, string name, string missingMessage)
    {
        if (!root.TryGetProperty("downloads", out var downloads) ||
            downloads.ValueKind != JsonValueKind.Object ||
            !downloads.TryGetProperty(name, out var entry) ||
            entry.ValueKind != JsonValueKind.Object ||
            !entry.TryGetProperty("url", out var urlValue) ||
            !Uri.TryCreate(urlValue.GetString(), UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException(missingMessage);
        }

        var sha1 = entry.TryGetProperty("sha1", out var sha1Value) ? sha1Value.GetString() : null;
        var size = entry.TryGetProperty("size", out var sizeValue) && sizeValue.TryGetInt64(out var parsedSize)
            ? parsedSize
            : (long?)null;
        return new MinecraftVersionDownload(url, sha1, size);
    }

    private async Task DownloadAndVerifyAsync(
        MinecraftVersionDownload download,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            download.Url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var destination = new FileStream(
                         temporaryPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         81920,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        var actualSize = new FileInfo(temporaryPath).Length;
        if (download.Size is { } expectedSize && actualSize != expectedSize)
        {
            throw new InvalidDataException($"下载文件长度不匹配：预期 {expectedSize}，实际 {actualSize}。");
        }

        if (string.IsNullOrWhiteSpace(download.Sha1))
        {
            return;
        }

        await using var stream = File.OpenRead(temporaryPath);
        var actualSha1 = Convert.ToHexString(await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        if (!string.Equals(actualSha1, download.Sha1, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("下载文件的 SHA-1 校验失败。");
        }
    }

    private static void ValidateVersion(MinecraftVersionCatalogEntry version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (string.IsNullOrWhiteSpace(version.Id) ||
            version.Id != Path.GetFileName(version.Id) ||
            version.MetadataUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("所选版本信息无效。");
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed record VersionMetadataDocument(string Json, JsonDocument Document) : IDisposable
    {
        public void Dispose() => Document.Dispose();
    }
}
