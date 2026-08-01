namespace PCL.Aurora.Platform.Abstractions;

public sealed record LauncherUpdateAsset(
    string Name,
    Uri DownloadUri,
    long Size,
    string? ContentType);

public sealed record LauncherUpdatePackage(
    LauncherUpdateAsset Archive,
    LauncherUpdateAsset Checksum);

public enum LauncherUpdateInstallStage
{
    Downloading = 0,
    Verifying = 1,
    Extracting = 2,
    Validating = 3,
    Ready = 4,
}

public sealed record LauncherUpdateInstallProgress(
    LauncherUpdateInstallStage Stage,
    string Message,
    double? Fraction = null);

public sealed record PreparedLauncherUpdate(
    string VersionName,
    string WorkingDirectory,
    string StagedApplicationPath);

public interface ILauncherUpdateInstaller
{
    bool IsSupported { get; }

    string? UnsupportedReason { get; }

    LauncherUpdatePackage SelectPackage(IReadOnlyList<LauncherUpdateAsset> assets);

    Task<PreparedLauncherUpdate> PrepareAsync(
        string versionName,
        LauncherUpdatePackage package,
        IProgress<LauncherUpdateInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task ScheduleInstallAndRestartAsync(
        PreparedLauncherUpdate update,
        CancellationToken cancellationToken = default);
}
