using System.IO.Compression;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class CommunityWorldImportService(IMinecraftDownloadExecutor downloadExecutor)
    : ICommunityWorldImportService
{
    private const string PackageFileName = ".pcl-aurora-world.zip";
    private const int MaximumArchiveEntries = 100_000;
    private const long MaximumFileSize = 4L * 1024 * 1024 * 1024;
    private const long MaximumExtractedSize = 20L * 1024 * 1024 * 1024;

    public async Task<CommunityWorldImportResult> ImportAsync(
        CommunityResourceProject project,
        CommunityResourceVersion version,
        string destinationDirectory,
        string worldName,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(version);
        if (project.Type != CommunityResourceType.World)
        {
            throw new InvalidOperationException("只有世界资源可以使用世界导入流程。");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (!IsSafeWorldName(worldName))
        {
            throw new InvalidDataException("世界文件夹名称包含文件系统不支持的字符。");
        }

        var parentDirectory = Path.GetFullPath(destinationDirectory);
        if (!Directory.Exists(parentDirectory))
        {
            throw new DirectoryNotFoundException("所选世界保存目录不存在。");
        }

        var targetDirectory = GetPathWithinRoot(parentDirectory, worldName);
        if (targetDirectory is null || File.Exists(targetDirectory) || Directory.Exists(targetDirectory))
        {
            throw new IOException($"目标目录中已存在 {worldName}，不会覆盖。");
        }

        var file = version.PrimaryFile ?? throw new InvalidOperationException("该世界版本没有可下载文件。");
        var stagingDirectory = Path.Combine(parentDirectory, $".{worldName}.{Guid.NewGuid():N}.partial");
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            await downloadExecutor.ExecuteAsync(
                new MinecraftDownloadPlan(
                    version.Id,
                    [new(project.DisplayTitle, PackageFileName, file.Url, file.Sha1, file.Size)],
                    []),
                stagingDirectory,
                progress,
                cancellationToken).ConfigureAwait(false);

            var packagePath = Path.Combine(stagingDirectory, PackageFileName);
            using var archive = ZipFile.OpenRead(packagePath);
            if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumArchiveEntries)
            {
                throw new InvalidDataException("世界压缩包为空或文件数量异常。");
            }

            var files = archive.Entries
                .Where(entry => !entry.FullName.EndsWith("/", StringComparison.Ordinal))
                .ToArray();
            var commonRoot = FindWorldRoot(files);
            var filesToExtract = commonRoot is null
                ? files
                : files.Where(entry => entry.FullName.StartsWith(commonRoot, StringComparison.Ordinal)).ToArray();
            long totalSize = 0;
            var extractedCount = 0;
            foreach (var entry in filesToExtract)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsSymbolicLink(entry) || entry.Length < 0 || entry.Length > MaximumFileSize ||
                    totalSize > MaximumExtractedSize - entry.Length)
                {
                    throw new InvalidDataException("世界压缩包中的文件大小或类型无效。");
                }

                var relativePath = commonRoot is null ? entry.FullName : entry.FullName[commonRoot.Length..];
                if (!IsSafeRelativePath(relativePath))
                {
                    throw new InvalidDataException("世界压缩包包含不安全的文件路径。");
                }

                var destinationPath = GetPathWithinRoot(
                    stagingDirectory,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (destinationPath is null || string.Equals(destinationPath, packagePath, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("世界压缩包中的文件超出目标目录。");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await using var source = entry.Open();
                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                totalSize += entry.Length;
                extractedCount++;
            }

            File.Delete(packagePath);
            if (!File.Exists(Path.Combine(stagingDirectory, "level.dat")))
            {
                throw new InvalidDataException("压缩包中没有可识别的 Minecraft 世界数据。");
            }

            if (File.Exists(targetDirectory) || Directory.Exists(targetDirectory))
            {
                throw new IOException($"目标目录中已存在 {worldName}，不会覆盖。");
            }

            Directory.Move(stagingDirectory, targetDirectory);
            return new(targetDirectory, extractedCount);
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    private static string? FindWorldRoot(IReadOnlyList<ZipArchiveEntry> files)
    {
        var levelDataCandidates = files.Where(entry =>
            entry.FullName.EndsWith("/level.dat", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (levelDataCandidates.Length != 1)
        {
            return null;
        }

        var levelDataPath = levelDataCandidates[0].FullName;
        return levelDataPath[..(levelDataPath.LastIndexOf('/') + 1)];
    }

    private static bool IsSafeWorldName(string value) =>
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
}
