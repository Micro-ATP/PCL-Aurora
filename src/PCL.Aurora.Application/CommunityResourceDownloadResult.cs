namespace PCL.Aurora.Application;

public sealed record CommunityResourceDownloadResult(
    IReadOnlyList<string> Paths,
    int DependencyCount);
