namespace PCL.Aurora.Domain;

public sealed record CommunityResourceVersionCatalog(
    IReadOnlyList<CommunityResourceVersion> Versions,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Errors.Count == 0;

    public static CommunityResourceVersionCatalog Failure(string error) => new([], [error]);
}
