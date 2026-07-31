namespace PCL.Aurora.Application;

public interface ILauncherLogService
{
    string LogDirectory { get; }

    string CurrentLogFilePath { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task AppendAsync(string category, string message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LauncherLogFile>> GetFilesAsync(CancellationToken cancellationToken = default);

    Task ExportAsync(IEnumerable<LauncherLogFile> files, string destinationPath, CancellationToken cancellationToken = default);

    Task<int> ClearHistoryAsync(CancellationToken cancellationToken = default);
}
