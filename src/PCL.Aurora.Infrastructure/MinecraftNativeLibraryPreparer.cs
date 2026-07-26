using System.IO.Compression;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Infrastructure;

public sealed class MinecraftNativeLibraryPreparer : INativeLibraryPreparer
{
    public async Task<MinecraftNativeLibraryPreparation> PrepareAsync(
        MinecraftNativeLibraryPlan nativeLibraryPlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nativeLibraryPlan);
        if (!nativeLibraryPlan.IsReady)
        {
            return new(nativeLibraryPlan, 0, nativeLibraryPlan.BlockingReasons
                .Concat(nativeLibraryPlan.MissingFiles.Select(path => $"缺少 native 文件：{path}"))
                .ToList());
        }

        try
        {
            EnsureDirectoryIsSafe(nativeLibraryPlan.NativesDirectory, nativeLibraryPlan.NativesDirectory);
            var extractedFileCount = 0;
            foreach (var archive in nativeLibraryPlan.Archives)
            {
                cancellationToken.ThrowIfCancellationRequested();
                extractedFileCount += await ExtractArchiveAsync(
                    archive.LocalPath,
                    nativeLibraryPlan.NativesDirectory,
                    cancellationToken).ConfigureAwait(false);
            }

            return new(nativeLibraryPlan, extractedFileCount, []);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(nativeLibraryPlan, 0, [$"native 解压失败：{exception.Message}"]);
        }
    }

    private static async Task<int> ExtractArchiveAsync(
        string archivePath,
        string nativesDirectory,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var extractedFileCount = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldSkip(entry.FullName))
            {
                continue;
            }

            var destinationPath = GetSafeDestinationPath(nativesDirectory, entry.FullName);
            var destinationDirectory = Path.GetDirectoryName(destinationPath)
                ?? throw new InvalidDataException("无法确定 native 文件的目标目录。");
            EnsureDirectoryIsSafe(destinationDirectory, nativesDirectory);
            var temporaryPath = Path.Combine(
                destinationDirectory,
                $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.partial");
            try
            {
                await using (var source = entry.Open())
                await using (var destination = new FileStream(
                                 temporaryPath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 bufferSize: 81920,
                                 useAsync: true))
                {
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporaryPath, destinationPath, overwrite: true);
                extractedFileCount++;
            }
            catch
            {
                TryDelete(temporaryPath);
                throw;
            }
        }

        return extractedFileCount;
    }

    private static bool ShouldSkip(string entryName)
    {
        var normalizedName = entryName.Replace('\\', '/');
        return string.IsNullOrWhiteSpace(normalizedName) ||
               normalizedName.EndsWith("/", StringComparison.Ordinal) ||
               normalizedName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSafeDestinationPath(string nativesDirectory, string entryName)
    {
        var normalizedName = entryName.Replace('\\', '/');
        if (normalizedName.StartsWith("/", StringComparison.Ordinal) ||
            normalizedName.Contains(':', StringComparison.Ordinal) ||
            normalizedName.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException("ZIP 条目包含不安全的 native 路径。");
        }

        var rootDirectory = Path.GetFullPath(nativesDirectory);
        var destinationPath = Path.GetFullPath(Path.Combine(rootDirectory, normalizedName));
        if (!IsWithinDirectory(destinationPath, rootDirectory))
        {
            throw new InvalidDataException("ZIP 条目不能位于 native 目录外。");
        }

        return destinationPath;
    }

    private static void EnsureDirectoryIsSafe(string directoryPath, string rootDirectory)
    {
        var root = Path.GetFullPath(rootDirectory);
        var target = Path.GetFullPath(directoryPath);
        if (!IsWithinDirectory(target, root) && !string.Equals(target, root, StringComparison.Ordinal))
        {
            throw new InvalidDataException("native 解压目录不能位于目标目录外。");
        }

        var relativePath = Path.GetRelativePath(root, target);
        var currentDirectory = root;
        if (!Directory.Exists(currentDirectory))
        {
            Directory.CreateDirectory(currentDirectory);
        }

        EnsureNotSymbolicLink(currentDirectory);
        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            currentDirectory = Path.Combine(currentDirectory, segment);
            if (!Directory.Exists(currentDirectory))
            {
                Directory.CreateDirectory(currentDirectory);
            }

            EnsureNotSymbolicLink(currentDirectory);
        }
    }

    private static void EnsureNotSymbolicLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("native 解压目录不能包含符号链接。");
        }
    }

    private static bool IsWithinDirectory(string path, string directory)
    {
        var directoryPrefix = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;
        return path.StartsWith(directoryPrefix, StringComparison.Ordinal);
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
