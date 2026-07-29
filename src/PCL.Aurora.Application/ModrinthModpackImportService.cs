using System.IO.Compression;
using System.Text.Json;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// Adapts PCL-CE's Modrinth pack flow to a transactional, cross-platform import.
/// Game and loader installation remain separate from importing the pack's client files.
/// </summary>
public sealed class ModrinthModpackImportService(IMinecraftDownloadExecutor downloadExecutor)
    : IModrinthModpackImportService
{
    private const string PackageFileName = ".pcl-aurora-source.mrpack";
    private const long MaximumIndexSize = 8 * 1024 * 1024;
    private const long MaximumOverrideFileSize = 1024L * 1024 * 1024;
    private const long MaximumOverrideTotalSize = 4L * 1024 * 1024 * 1024;
    private const long MaximumDownloadedTotalSize = 100L * 1024 * 1024 * 1024;
    private const int MaximumFiles = 20_000;
    private const int MaximumArchiveEntries = 100_000;

    public async Task<ModrinthModpackImportResult> ImportAsync(
        CommunityResourceProject project,
        CommunityResourceVersion version,
        string destinationDirectory,
        string instanceName,
        bool includeOptionalClientFiles = true,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(version);
        if (project.Type != CommunityResourceType.ModPack)
        {
            throw new InvalidOperationException("只有 Modrinth 整合包可以使用此导入流程。");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (!IsSafeInstanceName(instanceName))
        {
            throw new InvalidDataException("整合包名称包含文件系统不支持的字符。");
        }

        var parentDirectory = Path.GetFullPath(destinationDirectory);
        if (!Directory.Exists(parentDirectory))
        {
            throw new DirectoryNotFoundException("所选保存目录不存在。");
        }

        var targetDirectory = GetPathWithinRoot(parentDirectory, instanceName);
        if (targetDirectory is null || File.Exists(targetDirectory) || Directory.Exists(targetDirectory))
        {
            throw new IOException($"目标目录中已存在 {instanceName}，不会覆盖。");
        }

        var packageFile = version.PrimaryFile ?? throw new InvalidOperationException("该整合包版本没有可下载文件。");
        var stagingDirectory = Path.Combine(parentDirectory, $".{instanceName}.{Guid.NewGuid():N}.partial");
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            await downloadExecutor.ExecuteAsync(
                new MinecraftDownloadPlan(
                    version.Id,
                    [new(project.DisplayTitle, PackageFileName, packageFile.Url, packageFile.Sha1, packageFile.Size)],
                    []),
                stagingDirectory,
                progress,
                cancellationToken).ConfigureAwait(false);

            var packagePath = Path.Combine(stagingDirectory, PackageFileName);
            using var archive = ZipFile.OpenRead(packagePath);
            if (archive.Entries.Count > MaximumArchiveEntries)
            {
                throw new InvalidDataException("整合包中的文件数量过多，已停止导入。");
            }

            var (index, archiveBasePath) = await ReadIndexAsync(archive, cancellationToken).ConfigureAwait(false);
            var artifacts = BuildArtifacts(index, includeOptionalClientFiles);
            if (artifacts.Count > 0)
            {
                await downloadExecutor.ExecuteAsync(
                    new MinecraftDownloadPlan(version.Id, artifacts, []),
                    stagingDirectory,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            var overrideCount = await ExtractOverridesAsync(
                archive,
                archiveBasePath,
                stagingDirectory,
                cancellationToken).ConfigureAwait(false);
            File.Delete(packagePath);

            if (File.Exists(targetDirectory) || Directory.Exists(targetDirectory))
            {
                throw new IOException($"目标目录中已存在 {instanceName}，不会覆盖。");
            }

            Directory.Move(stagingDirectory, targetDirectory);
            return new(
                targetDirectory,
                index.MinecraftVersion,
                index.LoaderKind,
                index.LoaderVersion,
                artifacts.Count,
                overrideCount);
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    private static async Task<(PackIndex Index, string BasePath)> ReadIndexAsync(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var candidates = archive.Entries
            .Where(entry => IsIndexPath(entry.FullName))
            .ToArray();
        if (candidates.Length != 1)
        {
            throw new InvalidDataException("整合包必须包含唯一的 modrinth.index.json。");
        }

        var entry = candidates[0];
        if (entry.Length <= 0 || entry.Length > MaximumIndexSize || IsSymbolicLink(entry))
        {
            throw new InvalidDataException("Modrinth 整合包索引大小或类型无效。");
        }

        await using var stream = entry.Open();
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            GetInt32(root, "formatVersion") != 1 ||
            !string.Equals(GetString(root, "game"), "minecraft", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("仅支持 formatVersion 1 的 Minecraft Modrinth 整合包。");
        }

        if (!root.TryGetProperty("dependencies", out var dependencies) || dependencies.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(GetString(dependencies, "minecraft")))
        {
            throw new InvalidDataException("Modrinth 整合包未提供 Minecraft 版本信息。");
        }

        var minecraftVersion = GetString(dependencies, "minecraft")!;
        var loaderDependencies = new List<(MinecraftLoaderKind Kind, string Version)>();
        AddLoader("forge", MinecraftLoaderKind.Forge);
        AddLoader("neoforge", MinecraftLoaderKind.NeoForge);
        AddLoader("neo-forge", MinecraftLoaderKind.NeoForge);
        AddLoader("fabric-loader", MinecraftLoaderKind.Fabric);
        if (!string.IsNullOrWhiteSpace(GetString(dependencies, "quilt-loader")))
        {
            throw new InvalidDataException("当前版本尚不支持导入 Quilt 整合包。");
        }

        if (loaderDependencies.Count > 1)
        {
            throw new InvalidDataException("整合包同时声明了多个互斥加载器。");
        }

        var files = ParseFiles(root);
        var loader = loaderDependencies.FirstOrDefault();
        var basePath = entry.FullName[..^"modrinth.index.json".Length];
        return (new(minecraftVersion, loaderDependencies.Count == 0 ? null : loader.Kind,
            loaderDependencies.Count == 0 ? null : loader.Version, files), basePath);

        void AddLoader(string propertyName, MinecraftLoaderKind kind)
        {
            if (GetString(dependencies, propertyName) is { Length: > 0 } value)
            {
                loaderDependencies.Add((kind, value));
            }
        }
    }

    private static IReadOnlyList<PackFile> ParseFiles(JsonElement root)
    {
        if (!root.TryGetProperty("files", out var filesElement) || filesElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var files = new List<PackFile>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalSize = 0;
        foreach (var element in filesElement.EnumerateArray())
        {
            if (files.Count >= MaximumFiles || element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("整合包下载清单无效或文件数量过多。");
            }

            var path = GetString(element, "path");
            if (!IsSafeRelativePath(path) || !paths.Add(path!))
            {
                throw new InvalidDataException("整合包包含重复或不安全的下载路径。");
            }

            var size = GetInt64(element, "fileSize");
            if (size is null or <= 0)
            {
                throw new InvalidDataException($"{path} 的文件大小无效。");
            }

            if (totalSize > MaximumDownloadedTotalSize - size.Value)
            {
                throw new InvalidDataException("整合包声明的总下载大小异常，已停止导入。");
            }

            if (!element.TryGetProperty("hashes", out var hashes) || hashes.ValueKind != JsonValueKind.Object ||
                GetString(hashes, "sha1") is not { Length: 40 } sha1 || !sha1.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException($"{path} 缺少有效的 SHA-1 校验值。");
            }

            if (!element.TryGetProperty("downloads", out var downloads) || downloads.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"{path} 没有可用的下载地址。");
            }

            var urls = downloads.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri) ? uri : null)
                .Where(uri => uri?.Scheme == Uri.UriSchemeHttps)
                .OfType<Uri>()
                .Distinct()
                .ToArray();
            if (urls.Length == 0)
            {
                throw new InvalidDataException($"{path} 没有安全的 HTTPS 下载地址。");
            }

            var clientEnvironment = element.TryGetProperty("env", out var environment) &&
                                    environment.ValueKind == JsonValueKind.Object
                ? GetString(environment, "client")
                : null;
            files.Add(new(path!, urls, sha1, size.Value, clientEnvironment));
            totalSize += size.Value;
        }

        return files;
    }

    private static IReadOnlyList<MinecraftDownloadArtifact> BuildArtifacts(
        PackIndex index,
        bool includeOptionalClientFiles) =>
        index.Files
            .Where(file => !string.Equals(file.ClientEnvironment, "unsupported", StringComparison.OrdinalIgnoreCase))
            .Where(file => includeOptionalClientFiles ||
                           !string.Equals(file.ClientEnvironment, "optional", StringComparison.OrdinalIgnoreCase))
            .Select(file => new MinecraftDownloadArtifact(
                Path.GetFileName(file.Path),
                file.Path.Replace('/', Path.DirectorySeparatorChar),
                file.Urls[0],
                file.Sha1,
                file.Size,
                AlternativeUrls: file.Urls.Skip(1).ToArray()))
            .ToArray();

    private static async Task<int> ExtractOverridesAsync(
        ZipArchive archive,
        string basePath,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        var count = 0;
        long totalSize = 0;
        foreach (var overrideDirectory in new[] { "overrides/", "client-overrides/" })
        {
            var prefix = basePath + overrideDirectory;
            foreach (var entry in archive.Entries.Where(item => item.FullName.StartsWith(prefix, StringComparison.Ordinal)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsSymbolicLink(entry) || entry.Length < 0 || entry.Length > MaximumOverrideFileSize ||
                    totalSize > MaximumOverrideTotalSize - entry.Length)
                {
                    throw new InvalidDataException("整合包覆盖文件大小或类型无效。");
                }

                var relativePath = entry.FullName[prefix.Length..];
                if (!IsSafeRelativePath(relativePath))
                {
                    throw new InvalidDataException("整合包包含不安全的覆盖文件路径。");
                }

                var targetPath = GetPathWithinRoot(stagingDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (targetPath is null)
                {
                    throw new InvalidDataException("整合包覆盖文件超出目标目录。");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await using var source = entry.Open();
                await using var destination = new FileStream(
                    targetPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                totalSize += entry.Length;
                count++;
            }
        }

        return count;
    }

    private static bool IsIndexPath(string path) =>
        string.Equals(path, "modrinth.index.json", StringComparison.Ordinal) ||
        (!path.Contains('\\') && path.Count(character => character == '/') == 1 &&
         path.EndsWith("/modrinth.index.json", StringComparison.Ordinal));

    private static bool IsSafeInstanceName(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 120 && value is not "." and not ".." &&
        value == Path.GetFileName(value) && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        value.All(character => !char.IsControl(character) && "<>:\"/\\|?*".IndexOf(character) < 0);

    private static bool IsSafeRelativePath(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 512 && !value.Contains('\\') &&
        !value.StartsWith("/", StringComparison.Ordinal) &&
        value.Split('/').All(segment => segment.Length > 0 && segment is not "." and not ".." &&
                                          segment.All(character => !char.IsControl(character) &&
                                                                   "<>:\"|?*".IndexOf(character) < 0));

    private static string? GetPathWithinRoot(string rootDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return null;
        }

        var root = Path.GetFullPath(rootDirectory);
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? candidate
            : null;
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry) =>
        ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000;

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim()
            : null;

    private static int? GetInt32(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;

    private static long? GetInt64(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value)
            ? value
            : null;

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

    private sealed record PackIndex(
        string MinecraftVersion,
        MinecraftLoaderKind? LoaderKind,
        string? LoaderVersion,
        IReadOnlyList<PackFile> Files);

    private sealed record PackFile(
        string Path,
        IReadOnlyList<Uri> Urls,
        string Sha1,
        long Size,
        string? ClientEnvironment);
}
