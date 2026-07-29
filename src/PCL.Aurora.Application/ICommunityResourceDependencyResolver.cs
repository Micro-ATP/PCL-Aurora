using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface ICommunityResourceDependencyResolver
{
    Task<CommunityResourceDependencyPreparation> ResolveAsync(
        CommunityResourceVersion version,
        string? gameVersion,
        CommunityResourceLoader loader,
        CancellationToken cancellationToken = default);
}
