using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// Searches public community catalogs only after an explicit user action.
/// It does not download or install project files.
/// </summary>
public interface ICommunityResourceSearchService
{
    Task<CommunityResourceSearchResult> SearchAsync(
        CommunityResourceSearchRequest request,
        CancellationToken cancellationToken = default);
}
