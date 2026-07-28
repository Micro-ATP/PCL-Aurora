using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface ICommunityFavoritesStore
{
    Task<CommunityFavoritesLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyList<CommunityFavoriteFolder> folders,
        CancellationToken cancellationToken = default);
}

public sealed record CommunityFavoritesLoadResult(
    IReadOnlyList<CommunityFavoriteFolder> Folders,
    string? Warning);
