using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class CommunityResourceSearchService(
    ModrinthCommunityResourceSearchService modrinth,
    CurseForgeCommunityResourceSearchService curseForge) : ICommunityResourceSearchService
{
    public Task<CommunityResourceSearchResult> SearchAsync(
        CommunityResourceSearchRequest request,
        CancellationToken cancellationToken = default) =>
        request.Type == CommunityResourceType.World
            ? curseForge.SearchAsync(request, cancellationToken)
            : modrinth.SearchAsync(request, cancellationToken);
}
