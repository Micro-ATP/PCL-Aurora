using System.Security.Cryptography;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Infrastructure;

public sealed class MinecraftAssetMapper : IAssetMapper
{
    public async Task<MinecraftAssetMappingPreparation> PrepareAsync(
        MinecraftAssetMappingPlan mappingPlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mappingPlan);
        if (!mappingPlan.IsReady)
        {
            return new(mappingPlan, 0, mappingPlan.BlockingReasons
                .Concat(mappingPlan.MissingFiles.Select(path => $"缺少资源对象：{path}"))
                .ToList());
        }

        if (mappingPlan.Entries.Count == 0)
        {
            return new(mappingPlan, 0, []);
        }

        try
        {
            var mappedFileCount = 0;
            foreach (var entry in mappingPlan.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await VerifySourceAsync(entry, cancellationToken).ConfigureAwait(false);
                await CopyAtomicallyAsync(entry, mappingPlan.TargetDirectory!, cancellationToken).ConfigureAwait(false);
                mappedFileCount++;
            }

            return new(mappingPlan, mappedFileCount, []);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return new(mappingPlan, 0, [$"资源映射失败：{exception.Message}"]);
        }
    }

    private static async Task VerifySourceAsync(MinecraftAssetMappingEntry entry, CancellationToken cancellationToken)
    {
        if ((File.GetAttributes(entry.SourcePath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"资源对象 {entry.Asset.Name} 不能是符号链接。");
        }

        var fileInfo = new FileInfo(entry.SourcePath);
        if (fileInfo.Length != entry.Asset.Size)
        {
            throw new InvalidDataException($"资源对象 {entry.Asset.Name} 的文件大小校验失败。");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        await using var stream = new FileStream(
            entry.SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, read);
        }

        if (!string.Equals(Convert.ToHexString(hash.GetHashAndReset()), entry.Asset.Hash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"资源对象 {entry.Asset.Name} 的 SHA-1 校验失败。");
        }
    }

    private static async Task CopyAtomicallyAsync(
        MinecraftAssetMappingEntry entry,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(entry.DestinationPath)
            ?? throw new InvalidDataException("无法确定资源映射目标目录。");
        EnsureDirectoryIsSafe(destinationDirectory, targetDirectory);
        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(entry.DestinationPath)}.{Guid.NewGuid():N}.partial");
        try
        {
            await using (var source = new FileStream(
                             entry.SourcePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             bufferSize: 81920,
                             useAsync: true))
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

            File.Move(temporaryPath, entry.DestinationPath, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void EnsureDirectoryIsSafe(string directoryPath, string rootDirectory)
    {
        var root = Path.GetFullPath(rootDirectory);
        var target = Path.GetFullPath(directoryPath);
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, StringComparison.Ordinal) && !string.Equals(target, root, StringComparison.Ordinal))
        {
            throw new InvalidDataException("资源映射目录不能位于目标目录外。");
        }

        var relativePath = Path.GetRelativePath(root, target);
        var current = root;
        Directory.CreateDirectory(current);
        EnsureNotSymbolicLink(current);
        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            Directory.CreateDirectory(current);
            EnsureNotSymbolicLink(current);
        }
    }

    private static void EnsureNotSymbolicLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("资源映射目录不能包含符号链接。");
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
