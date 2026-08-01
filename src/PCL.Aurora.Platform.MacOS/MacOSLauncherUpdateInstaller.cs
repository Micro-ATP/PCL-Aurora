using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed partial class MacOSLauncherUpdateInstaller(
    HttpClient httpClient,
    IPlatformPaths platformPaths) : ILauncherUpdateInstaller
{
    private const long MaximumArchiveSize = 1_500_000_000;
    private const long MaximumExtractedSize = 4_000_000_000;
    private const int MaximumArchiveEntries = 50_000;
    private const long MaximumChecksumSize = 1_000_000;
    private readonly string? currentApplicationPath = ResolveCurrentApplicationPath();

    public bool IsSupported => OperatingSystem.IsMacOS() && currentApplicationPath is not null;

    public string? UnsupportedReason => IsSupported
        ? null
        : "当前不是从完整的 PCL Aurora.app 中运行，开发模式不能自动替换程序。";

    public LauncherUpdatePackage SelectPackage(IReadOnlyList<LauncherUpdateAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => throw new PlatformNotSupportedException("当前 macOS 处理器架构没有可用的更新包。"),
        };
        var platformTokens = new[] { $"osx-{architecture}", $"macos-{architecture}", $"mac-{architecture}" };
        var matchingArchives = assets.Where(asset =>
            asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            platformTokens.Any(token => asset.Name.Contains(token, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (matchingArchives.Length != 1)
        {
            throw new InvalidDataException(
                matchingArchives.Length == 0
                    ? $"发行版没有适用于 macOS {architecture} 的 ZIP 更新包。"
                    : $"发行版包含多个适用于 macOS {architecture} 的 ZIP 更新包，无法安全选择。");
        }
        var archive = matchingArchives[0];

        var checksum = assets.FirstOrDefault(asset =>
                asset.Name.Equals($"{archive.Name}.sha256", StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault(asset =>
                asset.Name.Equals("SHA256SUMS", StringComparison.OrdinalIgnoreCase) ||
                asset.Name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("发行版没有提供与更新包匹配的 SHA-256 校验清单。");
        return new LauncherUpdatePackage(archive, checksum);
    }

    public async Task<PreparedLauncherUpdate> PrepareAsync(
        string versionName,
        LauncherUpdatePackage package,
        IProgress<LauncherUpdateInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionName);
        ArgumentNullException.ThrowIfNull(package);
        EnsureSupported();
        ValidateAsset(package.Archive, MaximumArchiveSize, ".zip");
        ValidateAsset(package.Checksum, MaximumChecksumSize);

        var updateRoot = Path.Combine(platformPaths.Get().CacheDirectory, "Updates");
        Directory.CreateDirectory(updateRoot);
        var workingDirectory = Path.Combine(updateRoot, $"{SanitizeVersion(versionName)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        var archivePath = Path.Combine(workingDirectory, "update.zip");
        var checksumPath = Path.Combine(workingDirectory, "checksum.txt");
        var extractPath = Path.Combine(workingDirectory, "extracted");

        try
        {
            progress?.Report(new(LauncherUpdateInstallStage.Downloading, "正在下载更新包", 0));
            await DownloadAsync(package.Archive, archivePath, MaximumArchiveSize, progress, cancellationToken);
            await DownloadAsync(package.Checksum, checksumPath, MaximumChecksumSize, null, cancellationToken);

            progress?.Report(new(LauncherUpdateInstallStage.Verifying, "正在校验 SHA-256"));
            var expectedHash = await ReadExpectedHashAsync(
                checksumPath,
                package.Archive.Name,
                package.Checksum.Name.Equals($"{package.Archive.Name}.sha256", StringComparison.OrdinalIgnoreCase),
                cancellationToken);
            var actualHash = await ComputeSha256Async(archivePath, cancellationToken);
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("更新包 SHA-256 校验失败，已停止安装。");
            }

            progress?.Report(new(LauncherUpdateInstallStage.Extracting, "正在安全解压更新包"));
            await ExtractArchiveAsync(archivePath, extractPath, cancellationToken);
            var applications = Directory.EnumerateDirectories(extractPath, "PCL Aurora.app", SearchOption.AllDirectories).ToArray();
            if (applications.Length != 1)
            {
                throw new InvalidDataException("更新包必须且只能包含一个 PCL Aurora.app。");
            }

            progress?.Report(new(LauncherUpdateInstallStage.Validating, "正在验证应用程序结构"));
            ValidateApplicationBundle(applications[0]);
            progress?.Report(new(LauncherUpdateInstallStage.Ready, "更新已准备完成，等待重启安装", 1));
            return new PreparedLauncherUpdate(versionName, workingDirectory, applications[0]);
        }
        catch
        {
            TryDeleteDirectory(workingDirectory);
            throw;
        }
    }

    public async Task ScheduleInstallAndRestartAsync(
        PreparedLauncherUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        EnsureSupported();
        ValidateApplicationBundle(update.StagedApplicationPath);
        cancellationToken.ThrowIfCancellationRequested();

        var scriptPath = Path.Combine(update.WorkingDirectory, "install-update.sh");
        await File.WriteAllTextAsync(scriptPath, UpdateScript, cancellationToken);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(scriptPath);
        process.StartInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        process.StartInfo.ArgumentList.Add(currentApplicationPath!);
        process.StartInfo.ArgumentList.Add(update.StagedApplicationPath);
        process.StartInfo.ArgumentList.Add(update.WorkingDirectory);
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("无法启动更新辅助进程。");
        }
        process.Dispose();
    }

    private async Task DownloadAsync(
        LauncherUpdateAsset asset,
        string targetPath,
        long maximumSize,
        IProgress<LauncherUpdateInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUri);
        request.Headers.UserAgent.ParseAdd("PCL-Aurora/1.0");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength is > 0 && (declaredLength > maximumSize || asset.Size > 0 && declaredLength != asset.Size))
        {
            throw new InvalidDataException("更新资产长度与发行版元数据不一致。");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > maximumSize) throw new InvalidDataException("更新资产超过允许的最大大小。");
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            if (progress is not null)
            {
                var expected = declaredLength ?? (asset.Size > 0 ? asset.Size : 0);
                progress.Report(new(
                    LauncherUpdateInstallStage.Downloading,
                    expected > 0 ? $"正在下载更新包 {total * 100 / expected}%" : "正在下载更新包",
                    expected > 0 ? Math.Min(1, total / (double)expected) : null));
            }
        }

        if (asset.Size > 0 && total != asset.Size)
        {
            throw new InvalidDataException("更新资产下载长度与发行版元数据不一致。");
        }
    }

    private static async Task<string> ReadExpectedHashAsync(
        string checksumPath,
        string archiveName,
        bool allowUnnamedHash,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(checksumPath, cancellationToken);
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = Sha256LineRegex().Match(line);
            if (!match.Success) continue;
            var fileName = match.Groups["file"].Value.TrimStart('*').Trim();
            if (fileName.Length == 0 && allowUnnamedHash ||
                Path.GetFileName(fileName).Equals(archiveName, StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups["hash"].Value;
            }
        }

        throw new InvalidDataException("SHA-256 清单中没有当前更新包的校验值。");
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static async Task ExtractArchiveAsync(string archivePath, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        var destinationRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaximumArchiveEntries)
        {
            throw new InvalidDataException("更新包包含过多文件。");
        }
        long extractedSize = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            extractedSize = checked(extractedSize + entry.Length);
            if (extractedSize > MaximumExtractedSize)
            {
                throw new InvalidDataException("更新包解压后的总大小超过安全限制。");
            }
            var targetPath = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!targetPath.StartsWith(destinationRoot, StringComparison.Ordinal))
            {
                throw new InvalidDataException("更新包包含不安全的文件路径。");
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using var source = entry.Open();
            await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await source.CopyToAsync(target, cancellationToken);
        }
    }

    private static void ValidateApplicationBundle(string applicationPath)
    {
        var fullPath = Path.GetFullPath(applicationPath);
        if (!Directory.Exists(fullPath) || !fullPath.EndsWith("PCL Aurora.app", StringComparison.Ordinal))
        {
            throw new InvalidDataException("更新包中的应用名称不正确。");
        }
        var infoPlist = Path.Combine(fullPath, "Contents", "Info.plist");
        var executable = Path.Combine(fullPath, "Contents", "MacOS", "PCL.Aurora.Desktop");
        if (!File.Exists(infoPlist) || !File.Exists(executable) || new FileInfo(executable).Length <= 0)
        {
            throw new InvalidDataException("更新包缺少有效的 Info.plist 或主程序。");
        }
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(executable);
            File.SetUnixFileMode(executable, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
    }

    private static void ValidateAsset(LauncherUpdateAsset asset, long maximumSize, string? requiredSuffix = null)
    {
        if (asset.DownloadUri.Scheme != Uri.UriSchemeHttps || asset.Size < 0 || asset.Size > maximumSize ||
            requiredSuffix is not null && !asset.Name.EndsWith(requiredSuffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("发行版更新资产不符合安全要求。");
        }
    }

    private void EnsureSupported()
    {
        if (!IsSupported) throw new PlatformNotSupportedException(UnsupportedReason);
    }

    private static string? ResolveCurrentApplicationPath()
    {
        if (!OperatingSystem.IsMacOS() || string.IsNullOrWhiteSpace(Environment.ProcessPath)) return null;
        var directory = new FileInfo(Environment.ProcessPath).Directory;
        if (directory?.Name != "MacOS" || directory.Parent?.Name != "Contents") return null;
        var application = directory.Parent.Parent;
        return application is not null && application.Name.Equals("PCL Aurora.app", StringComparison.Ordinal)
            ? application.FullName
            : null;
    }

    private static string SanitizeVersion(string versionName) =>
        string.Concat(versionName.Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' ? character : '_'));

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    [GeneratedRegex("^(?<hash>[A-Fa-f0-9]{64})(?:\\s+[*]?(?<file>.+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256LineRegex();

    private const string UpdateScript = """
        #!/bin/sh
        parent_pid="$1"
        current_app="$2"
        staged_app="$3"
        working_dir="$4"
        backup_app="${current_app}.aurora-update-backup"

        attempts=0
        while kill -0 "$parent_pid" 2>/dev/null; do
          attempts=$((attempts + 1))
          [ "$attempts" -gt 300 ] && exit 10
          sleep 0.1
        done

        [ -e "$backup_app" ] && exit 11
        mv "$current_app" "$backup_app" || exit 12
        if ! mv "$staged_app" "$current_app"; then
          mv "$backup_app" "$current_app"
          exit 13
        fi
        if ! /usr/bin/open -n "$current_app"; then
          mv "$current_app" "$staged_app"
          mv "$backup_app" "$current_app"
          /usr/bin/open -n "$current_app"
          exit 14
        fi
        /bin/rm -rf "$backup_app"
        /bin/rm -rf "$working_dir"
        """;
}
