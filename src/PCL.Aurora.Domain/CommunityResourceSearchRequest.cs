namespace PCL.Aurora.Domain;

public sealed record CommunityResourceSearchRequest(
    CommunityResourceType Type,
    string SearchText,
    string? GameVersion,
    CommunityResourceLoader Loader,
    CommunityResourceSort Sort,
    int Page,
    int PageSize = 20);
