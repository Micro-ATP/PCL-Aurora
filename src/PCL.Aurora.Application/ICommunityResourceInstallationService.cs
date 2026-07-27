using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface ICommunityResourceInstallationService
{
    Task<CommunityResourceInstallationResult> InstallAsync(
        CommunityResourceProject project,
        CommunityResourceVersion version,
        MinecraftInstance instance,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
