using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface ICommunityResourceDownloadService
{
    Task<string> DownloadAsync(
        CommunityResourceProject project,
        CommunityResourceVersion version,
        string destinationDirectory,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
