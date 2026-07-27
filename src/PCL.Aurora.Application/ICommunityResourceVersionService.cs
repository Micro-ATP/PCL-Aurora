using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface ICommunityResourceVersionService
{
    Task<CommunityResourceVersionCatalog> GetProjectVersionsAsync(
        string projectId,
        string? gameVersion,
        CommunityResourceLoader loader,
        CancellationToken cancellationToken = default);

    Task<CommunityResourceVersionCatalog> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default);
}
