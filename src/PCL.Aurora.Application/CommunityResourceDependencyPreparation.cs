using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record CommunityResourceDependencyPreparation(
    IReadOnlyList<CommunityResourceVersion> RequiredVersions,
    IReadOnlyList<CommunityResourceOptionalDependency> OptionalDependencies,
    IReadOnlyList<string> Errors)
{
    public bool HasDependencies => RequiredVersions.Count > 0 || OptionalDependencies.Count > 0;
}
