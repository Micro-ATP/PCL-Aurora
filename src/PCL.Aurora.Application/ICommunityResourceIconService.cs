namespace PCL.Aurora.Application;

/// <summary>
/// Loads bounded project icons without blocking community search results.
/// </summary>
public interface ICommunityResourceIconService
{
    Task<byte[]?> LoadAsync(Uri iconUrl, CancellationToken cancellationToken = default);
}
