using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

/// <summary>
/// Installs a user-scoped Java runtime using PCL-CE's download, verify, stage,
/// rescan workflow. Azul is used because Mojang does not publish every Java
/// generation for macOS ARM64.
/// </summary>
public sealed class AzulJavaRuntimeInstallationService(
    HttpClient httpClient,
    IPlatformPaths platformPaths,
    IJavaInstallationInspector javaInstallationInspector) : IJavaRuntimeInstallationService
{
    private static readonly Uri PackageApiBaseUri = new("https://api.azul.com/metadata/v1/zulu/packages/");

    public async Task<JavaInstallation> InstallAsync(
        MinecraftJavaRequirement? requirement,
        IProgress<JavaRuntimeInstallationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var target = JavaRuntimeTargetResolver.Resolve(requirement);
        var majorVersion = target.MajorVersion;
        if (majorVersion is < 8 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(majorVersion), "Java 主版本必须介于 8 与 99 之间。");
        }

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("当前 Java 自动安装器仅支持 macOS。");
        }

        var architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "arm",
            Architecture.X64 => "x86",
            _ => throw new PlatformNotSupportedException($"暂不支持 {RuntimeInformation.OSArchitecture} 架构的 Java 自动安装。"),
        };
        progress?.Report(new("正在获取 Java 下载信息", 0.02));
        var package = await ResolvePackageAsync(target, architecture, cancellationToken).ConfigureAwait(false);

        var paths = platformPaths.Get();
        var runtimeRoot = Path.Combine(paths.ApplicationDataDirectory, "Runtimes");
        var cacheRoot = Path.Combine(paths.CacheDirectory, "Java");
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(cacheRoot);

        var operationId = Guid.NewGuid().ToString("N");
        var archivePath = Path.Combine(cacheRoot, $".{operationId}.tar.gz");
        var stagingPath = Path.Combine(runtimeRoot, $".{operationId}.partial");
        var destinationPath = Path.Combine(
            runtimeRoot,
            $"zulu-{package.Version}-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}");
        var backupPath = destinationPath + $".backup-{operationId}";
        var installed = false;
        var backupCreated = false;

        try
        {
            await DownloadAsync(package, archivePath, progress, cancellationToken).ConfigureAwait(false);
            progress?.Report(new("正在校验 Java 安装包", 0.82));
            await VerifyArchiveAsync(archivePath, package, cancellationToken).ConfigureAwait(false);

            progress?.Report(new("正在解压 Java", 0.86));
            Directory.CreateDirectory(stagingPath);
            await using (var archive = File.OpenRead(archivePath))
            await using (var gzip = new GZipStream(archive, CompressionMode.Decompress))
            {
                TarFile.ExtractToDirectory(gzip, stagingPath, overwriteFiles: false);
            }

            var stagedExecutable = FindJavaExecutable(stagingPath);
            EnsureExecutable(stagedExecutable);
            var stagedInstallation = await javaInstallationInspector
                .InspectAsync(stagedExecutable, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidDataException("下载的 Java 无法通过运行检查。");
            if (!stagedInstallation.IsCompatible ||
                stagedInstallation.ParsedVersion is not { } stagedVersion ||
                !target.Accepts(stagedVersion) ||
                (requirement is not null && requirement.GetBlockingReasons(stagedInstallation).Count > 0))
            {
                throw new InvalidDataException(
                    $"下载的 Java 版本或架构不匹配：期望 Java {majorVersion}，实际为 {stagedInstallation.Version ?? "未知"}。");
            }

            var relativeExecutablePath = Path.GetRelativePath(stagingPath, stagedExecutable);
            if (Directory.Exists(destinationPath))
            {
                Directory.Move(destinationPath, backupPath);
                backupCreated = true;
            }

            Directory.Move(stagingPath, destinationPath);
            installed = true;
            var installedExecutable = Path.Combine(destinationPath, relativeExecutablePath);
            var installation = await javaInstallationInspector
                .InspectAsync(installedExecutable, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidDataException("安装后的 Java 无法通过运行检查。");

            if (backupCreated)
            {
                Directory.Delete(backupPath, recursive: true);
                backupCreated = false;
            }

            progress?.Report(new($"Java {majorVersion} 安装完成", 1));
            return installation;
        }
        catch
        {
            if (installed && Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, recursive: true);
            }
            if (backupCreated && Directory.Exists(backupPath))
            {
                Directory.Move(backupPath, destinationPath);
            }
            throw;
        }
        finally
        {
            TryDeleteFile(archivePath);
            TryDeleteDirectory(stagingPath);
        }
    }

    private async Task<JavaPackage> ResolvePackageAsync(
        JavaRuntimeTarget target,
        string architecture,
        CancellationToken cancellationToken)
    {
        var query = $"?java_version={target.MajorVersion}&os=macos&arch={architecture}&hw_bitness=64" +
                    "&archive_type=tar.gz&java_package_type=jre&release_status=ga&availability_types=CA&page_size=200";
        using var response = await httpClient.GetAsync(new Uri(PackageApiBaseUri, query), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var candidate = document.RootElement
            .EnumerateArray()
            .Where(item => item.TryGetProperty("package_uuid", out _) && item.TryGetProperty("download_url", out _))
            .Where(item => ReadVersion(item) is { } version && target.Accepts(version))
            .OrderBy(item => item.GetProperty("name").GetString()?.Contains("-fx-", StringComparison.OrdinalIgnoreCase) == true)
            .ThenByDescending(item => ReadVersion(item))
            .FirstOrDefault();
        if (candidate.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Azul 当前没有适用于本机且满足版本约束的 Java {target.MajorVersion} JRE。");
        }

        var version = ReadVersion(candidate)
            ?? throw new InvalidDataException("Java 下载信息缺少版本号。");
        var packageId = candidate.GetProperty("package_uuid").GetString()
            ?? throw new InvalidDataException("Java 下载信息缺少包标识。");
        using var detailResponse = await httpClient.GetAsync(new Uri(PackageApiBaseUri, packageId), cancellationToken).ConfigureAwait(false);
        detailResponse.EnsureSuccessStatusCode();
        await using var detailStream = await detailResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var detailDocument = await JsonDocument.ParseAsync(detailStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var detail = detailDocument.RootElement;
        var downloadUrl = detail.GetProperty("download_url").GetString();
        var checksum = detail.GetProperty("sha256_hash").GetString();
        var name = detail.GetProperty("name").GetString();
        var size = detail.GetProperty("size").GetInt64();
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(checksum) || checksum.Length != 64 || string.IsNullOrWhiteSpace(name) || size <= 0)
        {
            throw new InvalidDataException("Java 下载信息不完整或不安全。");
        }

        return new JavaPackage(name, version, uri, checksum, size);
    }

    private async Task DownloadAsync(
        JavaPackage package,
        string destinationPath,
        IProgress<JavaRuntimeInstallationProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            package.DownloadUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        var buffer = new byte[81920];
        long downloaded = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            downloaded += read;
            progress?.Report(new(
                $"正在下载 {package.Name}（{downloaded / 1024d / 1024d:F1} / {package.Size / 1024d / 1024d:F1} MiB）",
                0.05 + Math.Clamp(downloaded / (double)package.Size, 0, 1) * 0.75));
        }
        await target.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task VerifyArchiveAsync(
        string archivePath,
        JavaPackage package,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(archivePath);
        if (file.Length != package.Size)
        {
            throw new InvalidDataException($"Java 安装包大小不匹配：期望 {package.Size}，实际 {file.Length}。");
        }
        await using var stream = file.OpenRead();
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        if (!string.Equals(actual, package.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Java 安装包 SHA-256 校验失败。");
        }
    }

    private static string FindJavaExecutable(string rootDirectory)
    {
        var executable = Directory
            .EnumerateFiles(rootDirectory, "java", SearchOption.AllDirectories)
            .FirstOrDefault(path => string.Equals(
                Path.GetFileName(Path.GetDirectoryName(path)),
                "bin",
                StringComparison.Ordinal));
        return executable ?? throw new InvalidDataException("Java 安装包中没有找到 bin/java。");
    }

    private static void EnsureExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
    }

    private static Version? ReadVersion(JsonElement item)
    {
        if (!item.TryGetProperty("java_version", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = value.EnumerateArray().Select(part => part.GetInt32()).Take(4).ToArray();
        return parts.Length switch
        {
            1 => new Version(parts[0], 0),
            2 => new Version(parts[0], parts[1]),
            3 => new Version(parts[0], parts[1], parts[2]),
            4 => new Version(parts[0], parts[1], parts[2], parts[3]),
            _ => null,
        };
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
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
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record JavaPackage(string Name, Version Version, Uri DownloadUri, string Sha256, long Size);
}
