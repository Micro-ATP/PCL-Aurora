using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

public interface ILauncherUpdateService
{
    Task<LauncherUpdateCheckResult> CheckAsync(
        string currentVersion,
        LauncherUpdateChannel channel,
        CancellationToken cancellationToken = default);
}

public sealed record LauncherUpdateCheckResult(
    bool IsUpdateAvailable,
    LauncherUpdateRelease LatestRelease);

public sealed record LauncherUpdateRelease(
    string VersionName,
    string DisplayName,
    string Summary,
    string Changelog,
    Uri ReleaseUri,
    DateTimeOffset PublishedAt,
    IReadOnlyList<LauncherUpdateAsset> Assets);
