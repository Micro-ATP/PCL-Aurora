using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record MinecraftModUpdateCandidate(
    MinecraftInstanceContentEntry LocalMod,
    CommunityResourceVersion CurrentVersion,
    CommunityResourceVersion LatestVersion)
{
    public string DisplayName => LocalMod.Name;

    public string VersionSummary =>
        $"{CurrentVersion.VersionNumber} → {LatestVersion.VersionNumber}";
}

public sealed record MinecraftModUpdateCheckResult(
    IReadOnlyList<MinecraftModUpdateCandidate> Updates,
    int RecognizedCount,
    int UnrecognizedCount,
    IReadOnlyList<string> Errors);

public sealed record MinecraftModUpdateApplyResult(int UpdatedCount, IReadOnlyList<string> UpdatedFiles);
