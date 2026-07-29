using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IModrinthModpackImportService
{
    Task<ModrinthModpackImportResult> ImportAsync(
        CommunityResourceProject project,
        CommunityResourceVersion version,
        string destinationDirectory,
        string instanceName,
        bool includeOptionalClientFiles = true,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
