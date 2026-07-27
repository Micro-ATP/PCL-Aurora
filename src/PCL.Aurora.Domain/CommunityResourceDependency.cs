namespace PCL.Aurora.Domain;

public sealed record CommunityResourceDependency(
    string? ProjectId,
    string? VersionId,
    string? FileName,
    CommunityResourceDependencyType Type);
