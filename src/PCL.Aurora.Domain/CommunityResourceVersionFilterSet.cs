namespace PCL.Aurora.Domain;

public sealed record CommunityResourceVersionFilterSet(
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<string> Loaders,
    bool GroupByMinorVersion,
    bool FoldLegacyVersions);
