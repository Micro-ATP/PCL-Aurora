namespace PCL.Aurora.Domain;

public sealed record CommunityResourceVersionGroup(
    string Title,
    IReadOnlyList<CommunityResourceVersion> Versions);
