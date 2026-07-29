using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record CommunityResourceOptionalDependency(
    string Id,
    string DisplayName,
    IReadOnlyList<CommunityResourceVersion> Versions);
