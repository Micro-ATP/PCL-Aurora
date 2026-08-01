namespace PCL.Aurora.Application;

public sealed class MinecraftJunkCleanupService : IMinecraftJunkCleanupService
{
    public Task<MinecraftJunkCleanupPlan> ScanAsync(
        string minecraftRootDirectory,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(minecraftRootDirectory, cancellationToken), cancellationToken);

    public Task<MinecraftJunkCleanupResult> CleanAsync(
        MinecraftJunkCleanupPlan plan,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Clean(plan, cancellationToken), cancellationToken);

    private static MinecraftJunkCleanupPlan Scan(string minecraftRootDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(minecraftRootDirectory))
        {
            throw new ArgumentException("Minecraft 目录不能为空。", nameof(minecraftRootDirectory));
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(minecraftRootDirectory));
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Minecraft 目录不存在：{root}");
        }

        if (IsSymbolicLink(root))
        {
            throw new InvalidOperationException("为避免跨目录删除，不能清理符号链接形式的 Minecraft 根目录。");
        }

        var entries = new List<MinecraftJunkCleanupEntry>();
        var workingDirectories = new List<string> { root };
        var versions = Path.Combine(root, "versions");
        if (Directory.Exists(versions) && !IsSymbolicLink(versions))
        {
            workingDirectories.AddRange(Directory.EnumerateDirectories(versions)
                .Where(directory => !IsSymbolicLink(directory)));
        }

        foreach (var directory in workingDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddDirectory(Path.Combine(directory, "crash-reports"));
            AddDirectory(Path.Combine(directory, "logs"));

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("hs_err_pid", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("WailaErrorOutput.txt", StringComparison.OrdinalIgnoreCase))
                {
                    AddFile(file);
                }
            }

            foreach (var nativeDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(nativeDirectory);
                if (name.EndsWith("-natives", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("natives-", StringComparison.OrdinalIgnoreCase))
                {
                    AddDirectory(nativeDirectory);
                }
            }
        }

        var distinctEntries = entries
            .GroupBy(entry => entry.Path, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return new(root, distinctEntries, distinctEntries.Sum(entry => entry.FileCount), distinctEntries.Sum(entry => entry.Size));

        void AddFile(string path)
        {
            if (!IsInsideRoot(root, path) || IsSymbolicLink(path))
            {
                return;
            }

            var info = new FileInfo(path);
            entries.Add(new(info.FullName, false, 1, Math.Max(0, info.Length)));
        }

        void AddDirectory(string path)
        {
            if (!Directory.Exists(path) || !IsInsideRoot(root, path) || IsSymbolicLink(path))
            {
                return;
            }

            if (!TrySummarizeDirectory(path, cancellationToken, out var fileCount, out var size))
            {
                return;
            }

            entries.Add(new(Path.GetFullPath(path), true, fileCount, size));
        }
    }

    private static MinecraftJunkCleanupResult Clean(MinecraftJunkCleanupPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(plan.RootDirectory));
        var approvedPaths = plan.Entries.Select(entry => Path.GetFullPath(entry.Path)).ToHashSet(StringComparer.Ordinal);
        var currentPlan = Scan(root, cancellationToken);
        var deletedEntries = 0;
        var deletedFiles = 0;
        long deletedBytes = 0;
        var failedEntries = 0;
        foreach (var entry in currentPlan.Entries
                     .Where(entry => approvedPaths.Contains(entry.Path))
                     .OrderByDescending(entry => entry.Path.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsInsideRoot(root, entry.Path) || IsSymbolicLink(entry.Path))
            {
                failedEntries++;
                continue;
            }

            try
            {
                if (entry.IsDirectory)
                {
                    if (!Directory.Exists(entry.Path))
                    {
                        continue;
                    }

                    if (!TrySummarizeDirectory(entry.Path, cancellationToken, out _, out _))
                    {
                        failedEntries++;
                        continue;
                    }

                    Directory.Delete(entry.Path, recursive: true);
                }
                else
                {
                    if (!File.Exists(entry.Path))
                    {
                        continue;
                    }

                    File.Delete(entry.Path);
                }

                deletedEntries++;
                deletedFiles += entry.FileCount;
                deletedBytes = checked(deletedBytes + entry.Size);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failedEntries++;
            }
        }

        return new(deletedEntries, deletedFiles, deletedBytes, failedEntries);
    }

    private static bool IsInsideRoot(string root, string path)
    {
        var fullPath = Path.GetFullPath(path);
        return !string.Equals(fullPath, root, StringComparison.Ordinal) &&
               fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static bool TrySummarizeDirectory(
        string root,
        CancellationToken cancellationToken,
        out int fileCount,
        out long size)
    {
        fileCount = 0;
        size = 0;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (IsSymbolicLink(file))
                {
                    return false;
                }

                fileCount++;
                size = checked(size + Math.Max(0, new FileInfo(file).Length));
            }

            foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (IsSymbolicLink(child))
                {
                    return false;
                }

                pending.Push(child);
            }
        }

        return true;
    }

    private static bool IsSymbolicLink(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}
