namespace PCL.Aurora.Application;

public sealed record CommunityResourceInstallationResult(
    int InstalledFileCount,
    int InstalledDependencyCount,
    IReadOnlyList<string> FileNames);
