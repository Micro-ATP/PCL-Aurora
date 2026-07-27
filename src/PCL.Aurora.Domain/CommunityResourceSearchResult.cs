namespace PCL.Aurora.Domain;

public sealed record CommunityResourceSearchResult(
    IReadOnlyList<CommunityResourceProject> Projects,
    int Offset,
    int Limit,
    int TotalHits,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Errors.Count == 0;

    public bool HasNextPage => Offset + Projects.Count < TotalHits;

    public static CommunityResourceSearchResult Failure(string error) =>
        new([], 0, 0, 0, [error]);
}
