using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface ICommunityWorldImportService
{
    Task<CommunityWorldImportResult> ImportAsync(
        CommunityResourceProject project,
        CommunityResourceVersion version,
        string destinationDirectory,
        string worldName,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record CommunityWorldImportResult(string WorldDirectory, int ExtractedFileCount);
