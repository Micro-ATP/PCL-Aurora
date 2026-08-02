using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftInstanceManagementService : IMinecraftInstanceManagementService
{
    private const string ProfileRelativePath = ".pcl-aurora/instance.json";
    private const int MaximumTopLevelEntries = 100_000;
    private const int MaximumArchiveFiles = 500_000;
    private static readonly JsonSerializerOptions ProfileJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public Task<MinecraftInstanceProfile> GetProfileAsync(
        MinecraftInstance instance,
        CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(instance, MinecraftInstanceIsolationMode.All);
        return LoadProfileAsync(paths.InstanceDirectory, cancellationToken);
    }

    public async Task<MinecraftInstanceManagementSnapshot> InspectAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        CancellationToken cancellationToken = default)
    {
        var initialPaths = ResolvePaths(instance, isolationMode);
        var profile = await LoadProfileAsync(initialPaths.InstanceDirectory, cancellationToken).ConfigureAwait(false);
        var effectiveIsolationMode = profile.IsolationMode ?? isolationMode;
        var paths = effectiveIsolationMode == isolationMode
            ? initialPaths
            : ResolvePaths(instance, effectiveIsolationMode);
        var counts = new Dictionary<MinecraftInstanceContentKind, int>();
        foreach (var kind in Enum.GetValues<MinecraftInstanceContentKind>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            counts[kind] = (await GetContentAsync(instance, effectiveIsolationMode, kind, cancellationToken)
                .ConfigureAwait(false)).Count;
        }

        var servers = await GetServersAsync(instance, effectiveIsolationMode, cancellationToken).ConfigureAwait(false);
        return new(instance, paths.MinecraftRootDirectory, paths.GameDirectory, effectiveIsolationMode, profile, counts, servers.Count);
    }

    public Task<IReadOnlyList<MinecraftInstanceContentEntry>> GetContentAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind,
        CancellationToken cancellationToken = default)
    {
        var directory = GetContentDirectory(instance, isolationMode, kind);
        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<MinecraftInstanceContentEntry>>([]);
        }

        return Task.Run<IReadOnlyList<MinecraftInstanceContentEntry>>(() =>
        {
            var result = new List<MinecraftInstanceContentEntry>();
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos().Take(MaximumTopLevelEntries + 1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (result.Count >= MaximumTopLevelEntries)
                {
                    throw new IOException($"{GetKindDisplay(kind)}目录中的项目过多，已停止读取。");
                }
                if (IsReparsePoint(entry))
                {
                    continue;
                }
                if (!ShouldInclude(kind, entry))
                {
                    continue;
                }

                var isDirectory = entry is DirectoryInfo;
                var enabled = kind != MinecraftInstanceContentKind.Mod ||
                              !entry.Name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                var displayName = enabled ? entry.Name : entry.Name[..^".disabled".Length];
                var size = entry is FileInfo file ? file.Length : GetDirectorySize(entry.FullName, cancellationToken);
                result.Add(new(
                    kind,
                    displayName,
                    entry.Name,
                    entry.FullName,
                    isDirectory,
                    size,
                    entry.LastWriteTimeUtc,
                    enabled,
                    CreateDetail(kind, entry, size, enabled)));
            }

            return result
                .OrderByDescending(item => item.IsEnabled)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }, cancellationToken);
    }

    public async Task<MinecraftInstanceImportResult> ImportAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind,
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        if (sourcePaths.Count is 0 or > 512)
        {
            throw new InvalidDataException("请选择 1 到 512 个文件或文件夹。");
        }

        var destinationDirectory = GetContentDirectory(instance, isolationMode, kind);
        Directory.CreateDirectory(destinationDirectory);
        var imported = new List<string>();
        foreach (var sourcePath in sourcePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = Path.GetFullPath(sourcePath);
            var sourceInfo = GetExistingFileSystemInfo(source);
            if (IsReparsePoint(sourceInfo))
            {
                throw new InvalidDataException($"不能导入符号链接：{sourceInfo.Name}");
            }
            if (!ShouldInclude(kind, sourceInfo))
            {
                throw new InvalidDataException($"{sourceInfo.Name} 不是可识别的{GetKindDisplay(kind)}项目。");
            }

            var name = sourceInfo.Name;
            var destination = GetPathWithinRoot(destinationDirectory, name);
            if (File.Exists(destination) || Directory.Exists(destination))
            {
                throw new IOException($"目标目录中已存在 {name}，不会覆盖。");
            }
            if (IsSameOrNestedPath(source, destinationDirectory))
            {
                throw new InvalidOperationException($"{name} 已位于目标目录中。");
            }

            var staging = GetPathWithinRoot(destinationDirectory, $".pcl-aurora-import-{Guid.NewGuid():N}.partial");
            try
            {
                if (sourceInfo is DirectoryInfo)
                {
                    await CopyDirectoryAsync(source, staging, cancellationToken).ConfigureAwait(false);
                    Directory.Move(staging, destination);
                }
                else
                {
                    await CopyFileAsync(source, staging, cancellationToken).ConfigureAwait(false);
                    File.Move(staging, destination);
                }
                imported.Add(name);
            }
            catch
            {
                TryDeletePath(staging);
                throw;
            }
        }

        return new(imported.Count, imported);
    }

    public Task SetContentEnabledAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind,
        string relativePath,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (kind != MinecraftInstanceContentKind.Mod)
        {
            throw new InvalidOperationException("只有 Mod 支持启用或禁用。");
        }

        var directory = GetContentDirectory(instance, isolationMode, kind);
        var source = ResolveExistingEntry(directory, relativePath);
        var isDisabled = source.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
        if (enabled == !isDisabled)
        {
            return Task.CompletedTask;
        }

        var destination = enabled ? source[..^".disabled".Length] : source + ".disabled";
        EnsureDirectChild(directory, destination);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException($"目标名称 {Path.GetFileName(destination)} 已存在。");
        }
        MovePath(source, destination);
        return Task.CompletedTask;
    }

    public Task DeleteContentAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = GetContentDirectory(instance, isolationMode, kind);
        var path = ResolveExistingEntry(directory, relativePath);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    public async Task ExportContentAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind,
        string relativePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var directory = GetContentDirectory(instance, isolationMode, kind);
        var source = ResolveExistingEntry(directory, relativePath);
        var destination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var staging = destination + $".{Guid.NewGuid():N}.partial";
        try
        {
            if (Directory.Exists(source))
            {
                await CreateArchiveAsync([(source, string.Empty)], staging, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await CopyFileAsync(source, staging, cancellationToken).ConfigureAwait(false);
            }
            File.Move(staging, destination, overwrite: true);
        }
        catch
        {
            TryDeletePath(staging);
            throw;
        }
    }

    public Task<IReadOnlyList<MinecraftServerEntry>> GetServersAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(instance, isolationMode);
        var path = Path.Combine(paths.GameDirectory, "servers.dat");
        return Task.Run(() => MinecraftServerListCodec.Read(path), cancellationToken);
    }

    public Task SaveServersAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        IReadOnlyList<MinecraftServerEntry> servers,
        CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(instance, isolationMode);
        var path = Path.Combine(paths.GameDirectory, "servers.dat");
        return Task.Run(() => MinecraftServerListCodec.Write(path, servers), cancellationToken);
    }

    public async Task SaveProfileAsync(
        MinecraftInstance instance,
        MinecraftInstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var paths = ResolvePaths(instance, MinecraftInstanceIsolationMode.All);
        if (!profile.IsValid)
        {
            throw new InvalidDataException("实例描述过长。");
        }

        var path = Path.Combine(paths.InstanceDirectory, ProfileRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + $".{Guid.NewGuid():N}.partial";
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, profile, ProfileJsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            TryDeletePath(temporaryPath);
            throw;
        }
    }

    public async Task<string> RenameAsync(
        MinecraftInstance instance,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(instance, MinecraftInstanceIsolationMode.All);
        ValidateInstanceName(newName);
        if (string.Equals(instance.Name, newName, StringComparison.Ordinal))
        {
            return paths.InstanceDirectory;
        }

        var target = GetPathWithinRoot(paths.VersionsDirectory, newName);
        EnsureDestinationAvailable(target, newName);
        var staging = GetPathWithinRoot(paths.VersionsDirectory, $".pcl-aurora-rename-{Guid.NewGuid():N}.partial");
        try
        {
            await CopyDirectoryAsync(paths.InstanceDirectory, staging, cancellationToken).ConfigureAwait(false);
            await RewriteInstanceIdentityAsync(staging, instance.Name, newName, cancellationToken).ConfigureAwait(false);
            Directory.Move(staging, target);
            try
            {
                Directory.Delete(paths.InstanceDirectory, recursive: true);
            }
            catch
            {
                TryDeletePath(target);
                throw;
            }
            return target;
        }
        catch
        {
            TryDeletePath(staging);
            throw;
        }
    }

    public async Task<string> CopyAsync(
        MinecraftInstance instance,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(instance, MinecraftInstanceIsolationMode.All);
        ValidateInstanceName(newName);
        var target = GetPathWithinRoot(paths.VersionsDirectory, newName);
        EnsureDestinationAvailable(target, newName);
        var staging = GetPathWithinRoot(paths.VersionsDirectory, $".pcl-aurora-copy-{Guid.NewGuid():N}.partial");
        try
        {
            await CopyDirectoryAsync(paths.InstanceDirectory, staging, cancellationToken).ConfigureAwait(false);
            await RewriteInstanceIdentityAsync(staging, instance.Name, newName, cancellationToken).ConfigureAwait(false);
            Directory.Move(staging, target);
            return target;
        }
        catch
        {
            TryDeletePath(staging);
            throw;
        }
    }

    public async Task<MinecraftInstanceArchiveResult> ExportInstanceAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        string destinationPath,
        bool includeGameData,
        CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(instance, isolationMode);
        var destination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var staging = destination + $".{Guid.NewGuid():N}.partial";
        var roots = new List<(string Source, string Prefix)>
        {
            (paths.InstanceDirectory, $"versions/{instance.Name}"),
        };
        if (includeGameData && !PathEquals(paths.GameDirectory, paths.InstanceDirectory))
        {
            foreach (var name in new[]
                     {
                         "mods", "resourcepacks", "shaderpacks", "saves", "screenshots", "schematics",
                     })
            {
                var source = Path.Combine(paths.GameDirectory, name);
                if (Directory.Exists(source))
                {
                    roots.Add((source, $"game/{name}"));
                }
            }
            foreach (var name in new[] { "options.txt", "servers.dat" })
            {
                var source = Path.Combine(paths.GameDirectory, name);
                if (File.Exists(source))
                {
                    roots.Add((source, $"game/{name}"));
                }
            }
        }

        try
        {
            var result = await CreateArchiveAsync(roots, staging, cancellationToken).ConfigureAwait(false);
            File.Move(staging, destination, overwrite: true);
            return new(destination, result.FileCount, result.TotalBytes);
        }
        catch
        {
            TryDeletePath(staging);
            throw;
        }
    }

    public Task DeleteInstanceAsync(MinecraftInstance instance, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paths = ResolvePaths(instance, MinecraftInstanceIsolationMode.All);
        Directory.Delete(paths.InstanceDirectory, recursive: true);
        return Task.CompletedTask;
    }

    public string GetContentDirectory(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        MinecraftInstanceContentKind kind)
    {
        var paths = ResolvePaths(instance, isolationMode);
        return Path.Combine(paths.GameDirectory, kind switch
        {
            MinecraftInstanceContentKind.Mod => "mods",
            MinecraftInstanceContentKind.ResourcePack => "resourcepacks",
            MinecraftInstanceContentKind.ShaderPack => "shaderpacks",
            MinecraftInstanceContentKind.Save => "saves",
            MinecraftInstanceContentKind.Screenshot => "screenshots",
            MinecraftInstanceContentKind.Schematic => "schematics",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        });
    }

    private static async Task<MinecraftInstanceProfile> LoadProfileAsync(
        string instanceDirectory,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(instanceDirectory, ProfileRelativePath);
        if (!File.Exists(path))
        {
            return MinecraftInstanceProfile.Default;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var profile = await JsonSerializer.DeserializeAsync<MinecraftInstanceProfile>(
                stream,
                ProfileJsonOptions,
                cancellationToken).ConfigureAwait(false);
            return profile is { IsValid: true } ? profile : MinecraftInstanceProfile.Default;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return MinecraftInstanceProfile.Default;
        }
    }

    private static async Task RewriteInstanceIdentityAsync(
        string directory,
        string oldName,
        string newName,
        CancellationToken cancellationToken)
    {
        foreach (var extension in new[] { ".json", ".jar" })
        {
            var source = Path.Combine(directory, oldName + extension);
            var destination = Path.Combine(directory, newName + extension);
            if (File.Exists(source))
            {
                File.Move(source, destination);
            }
        }

        var oldNatives = Path.Combine(directory, oldName + "-natives");
        if (Directory.Exists(oldNatives))
        {
            Directory.Move(oldNatives, Path.Combine(directory, newName + "-natives"));
        }

        var metadataPath = Path.Combine(directory, newName + ".json");
        if (!File.Exists(metadataPath))
        {
            return;
        }
        var json = await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false);
        var root = JsonNode.Parse(json) as JsonObject
                   ?? throw new InvalidDataException("实例版本 JSON 不是对象。");
        root["id"] = newName;
        await File.WriteAllTextAsync(
            metadataPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(int FileCount, long TotalBytes)> CreateArchiveAsync(
        IReadOnlyList<(string Source, string Prefix)> roots,
        string destination,
        CancellationToken cancellationToken)
    {
        var fileCount = 0;
        long totalBytes = 0;
        await using var file = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var (source, prefix) in roots)
        {
            if (File.Exists(source))
            {
                await AddFileToArchiveAsync(source, prefix, archive, cancellationToken).ConfigureAwait(false);
                fileCount++;
                totalBytes += new FileInfo(source).Length;
                continue;
            }

            foreach (var path in EnumerateFilesSafe(source))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++fileCount > MaximumArchiveFiles)
                {
                    throw new IOException("实例文件数量过多，已停止归档。");
                }
                var relative = Path.GetRelativePath(source, path).Replace(Path.DirectorySeparatorChar, '/');
                await AddFileToArchiveAsync(
                    path,
                    string.IsNullOrEmpty(prefix) ? relative : $"{prefix.TrimEnd('/')}/{relative}",
                    archive,
                    cancellationToken).ConfigureAwait(false);
                totalBytes += new FileInfo(path).Length;
            }
        }
        return (fileCount, totalBytes);
    }

    private static async Task AddFileToArchiveAsync(
        string source,
        string entryName,
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Optimal);
        await using var input = new FileStream(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = entry.Open();
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if (IsReparsePoint(entry))
                {
                    throw new InvalidDataException($"实例包含符号链接，无法安全归档：{entry.FullName}");
                }
                if (entry is DirectoryInfo)
                {
                    pending.Push(entry.FullName);
                }
                else
                {
                    yield return entry.FullName;
                }
            }
        }
    }

    private static async Task CopyDirectoryAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsReparsePoint(entry))
            {
                throw new InvalidDataException($"不能复制符号链接：{entry.FullName}");
            }
            var target = Path.Combine(destination, entry.Name);
            if (entry is DirectoryInfo)
            {
                await CopyDirectoryAsync(entry.FullName, target, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await CopyFileAsync(entry.FullName, target, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(
            destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static long GetDirectorySize(string directory, CancellationToken cancellationToken)
    {
        long total = 0;
        var count = 0;
        foreach (var path in EnumerateFilesSafe(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++count > MaximumArchiveFiles)
            {
                return total;
            }
            total = checked(total + new FileInfo(path).Length);
        }
        return total;
    }

    private static bool ShouldInclude(MinecraftInstanceContentKind kind, FileSystemInfo entry) => kind switch
    {
        MinecraftInstanceContentKind.Save => entry is DirectoryInfo,
        MinecraftInstanceContentKind.Screenshot => entry is FileInfo &&
            entry.Extension.ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp",
        MinecraftInstanceContentKind.Mod => entry is DirectoryInfo ||
            entry.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
            entry.Name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase) ||
            entry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            entry.Name.EndsWith(".zip.disabled", StringComparison.OrdinalIgnoreCase) ||
            entry.Name.EndsWith(".litemod", StringComparison.OrdinalIgnoreCase) ||
            entry.Name.EndsWith(".litemod.disabled", StringComparison.OrdinalIgnoreCase),
        _ => entry is DirectoryInfo || entry is FileInfo,
    };

    private static string CreateDetail(
        MinecraftInstanceContentKind kind,
        FileSystemInfo entry,
        long size,
        bool enabled)
    {
        var status = kind == MinecraftInstanceContentKind.Mod ? (enabled ? "已启用" : "已禁用") :
            entry is DirectoryInfo ? "文件夹" : "文件";
        return $"{status} · {FormatBytes(size)} · {entry.LastWriteTime:yyyy/M/d HH:mm}";
    }

    private static string FormatBytes(long value)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var amount = (double)Math.Max(0, value);
        var unit = 0;
        while (amount >= 1024 && unit < units.Length - 1)
        {
            amount /= 1024;
            unit++;
        }
        return unit == 0 ? $"{amount:0} {units[unit]}" : $"{amount:0.##} {units[unit]}";
    }

    private static string GetKindDisplay(MinecraftInstanceContentKind kind) => kind switch
    {
        MinecraftInstanceContentKind.Mod => "Mod",
        MinecraftInstanceContentKind.ResourcePack => "资源包",
        MinecraftInstanceContentKind.ShaderPack => "光影包",
        MinecraftInstanceContentKind.Save => "存档",
        MinecraftInstanceContentKind.Screenshot => "截图",
        MinecraftInstanceContentKind.Schematic => "投影原理图",
        _ => "资源",
    };

    private static ResolvedInstancePaths ResolvePaths(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.Status != MinecraftInstanceStatus.Valid)
        {
            throw new InvalidOperationException("只能管理可读取的 Minecraft 实例。");
        }

        var instanceDirectory = Path.GetFullPath(instance.DirectoryPath);
        if (!Directory.Exists(instanceDirectory) || IsReparsePoint(new DirectoryInfo(instanceDirectory)))
        {
            throw new DirectoryNotFoundException("实例目录不存在或是符号链接。");
        }
        var versionsDirectory = Directory.GetParent(instanceDirectory)?.FullName
                                ?? throw new InvalidOperationException("无法确定版本目录。");
        var minecraftRootDirectory = Directory.GetParent(versionsDirectory)?.FullName
                                     ?? throw new InvalidOperationException("无法确定 Minecraft 根目录。");
        EnsureDirectChild(versionsDirectory, instanceDirectory);
        var gameDirectory = Path.GetFullPath(MinecraftInstanceIsolationResolver.ResolveGameDirectory(
            instance,
            minecraftRootDirectory,
            isolationMode));
        if (!PathEquals(gameDirectory, minecraftRootDirectory) && !PathEquals(gameDirectory, instanceDirectory))
        {
            throw new InvalidDataException("解析出的实例游戏目录超出允许边界。");
        }
        return new(instanceDirectory, versionsDirectory, minecraftRootDirectory, gameDirectory);
    }

    private static FileSystemInfo GetExistingFileSystemInfo(string path)
    {
        if (File.Exists(path))
        {
            return new FileInfo(path);
        }
        if (Directory.Exists(path))
        {
            return new DirectoryInfo(path);
        }
        throw new FileNotFoundException("所选文件或文件夹不存在。", path);
    }

    private static string ResolveExistingEntry(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath != Path.GetFileName(relativePath))
        {
            throw new InvalidDataException("资源路径无效。");
        }
        var path = GetPathWithinRoot(root, relativePath);
        EnsureDirectChild(root, path);
        var info = GetExistingFileSystemInfo(path);
        if (IsReparsePoint(info))
        {
            throw new InvalidDataException("不能操作符号链接资源。");
        }
        return path;
    }

    private static string GetPathWithinRoot(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("目标路径必须是相对路径。");
        }
        var fullRoot = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!candidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException("目标路径超出允许目录。");
        }
        return candidate;
    }

    private static void EnsureDirectChild(string root, string path)
    {
        var parent = Directory.GetParent(Path.GetFullPath(path))?.FullName;
        if (parent is null || !PathEquals(parent, Path.GetFullPath(root)))
        {
            throw new InvalidDataException("目标不是允许目录的直接子项。");
        }
    }

    private static bool IsSameOrNestedPath(string candidate, string root)
    {
        var fullCandidate = Path.GetFullPath(candidate);
        var fullRoot = Path.GetFullPath(root);
        return PathEquals(fullCandidate, fullRoot) ||
               fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.Ordinal);

    private static bool IsReparsePoint(FileSystemInfo info) =>
        info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget is not null;

    private static void MovePath(string source, string destination)
    {
        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
        }
        else
        {
            File.Move(source, destination);
        }
    }

    private static void EnsureDestinationAvailable(string target, string name)
    {
        if (File.Exists(target) || Directory.Exists(target))
        {
            throw new IOException($"实例 {name} 已存在。");
        }
    }

    private static void ValidateInstanceName(string value)
    {
        if (!LauncherPreferences.IsValidInstanceName(value) ||
            value.StartsWith(".pcl-aurora-", StringComparison.OrdinalIgnoreCase) ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException("实例名称为空、过长或包含文件系统不支持的字符。");
        }
    }

    private static void TryDeletePath(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record ResolvedInstancePaths(
        string InstanceDirectory,
        string VersionsDirectory,
        string MinecraftRootDirectory,
        string GameDirectory);
}
