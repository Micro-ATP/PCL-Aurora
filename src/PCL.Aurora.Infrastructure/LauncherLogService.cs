using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using PCL.Aurora.Application;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Infrastructure;

public sealed class LauncherLogService : ILauncherLogService, IDisposable
{
    private readonly SemaphoreSlim fileGate = new(1, 1);
    private bool initialized;

    public LauncherLogService(IPlatformPaths platformPaths)
    {
        ArgumentNullException.ThrowIfNull(platformPaths);
        LogDirectory = Path.Combine(platformPaths.Get().ApplicationDataDirectory, "Logs");
        CurrentLogFilePath = Path.Combine(
            LogDirectory,
            $"Launch-{DateTime.Now:yyyy-M-d-HHmmssfff}.log");
    }

    public string LogDirectory { get; }

    public string CurrentLogFilePath { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized)
            {
                return;
            }

            Directory.CreateDirectory(LogDirectory);
            var version = typeof(LauncherLogService).Assembly.GetName().Version?.ToString(3) ?? "未知";
            var header = string.Join(Environment.NewLine,
            [
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [System] PCL Aurora {version} 启动",
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [System] {RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}",
                string.Empty,
            ]);
            await File.WriteAllTextAsync(CurrentLogFilePath, header, new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);
            initialized = true;
        }
        finally
        {
            fileGate.Release();
        }
    }

    public async Task AppendAsync(string category, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(message);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var lines = message.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            builder.Append('[').Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
                .Append("] [").Append(category.Trim()).Append("] ").AppendLine(line);
        }

        await fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(CurrentLogFilePath, builder.ToString(), new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            fileGate.Release();
        }
    }

    public async Task<IReadOnlyList<LauncherLogFile>> GetFilesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return Directory.EnumerateFiles(LogDirectory, "*.log", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new LauncherLogFile(
                file.Name,
                file.FullName,
                new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero).ToLocalTime(),
                file.Length,
                string.Equals(file.FullName, CurrentLogFilePath, StringComparison.Ordinal)))
            .ToArray();
    }

    public async Task ExportAsync(
        IEnumerable<LauncherLogFile> files,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var selectedFiles = files
            .Where(file => File.Exists(file.FullPath))
            .DistinctBy(file => file.FullPath, StringComparer.Ordinal)
            .ToArray();
        if (selectedFiles.Length == 0)
        {
            throw new InvalidOperationException("没有可导出的日志。");
        }

        var parent = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        await fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var destination = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);
            using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
            foreach (var file in selectedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = archive.CreateEntry(file.Name, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var source = new FileStream(
                    file.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            fileGate.Release();
        }
    }

    public async Task<int> ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        var files = await GetFilesAsync(cancellationToken).ConfigureAwait(false);
        var deleted = 0;
        foreach (var file in files.Where(file => !file.IsCurrent))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(file.FullPath);
            deleted++;
        }

        return deleted;
    }

    public void Dispose() => fileGate.Dispose();
}
