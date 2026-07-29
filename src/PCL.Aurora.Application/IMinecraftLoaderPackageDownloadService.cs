using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftLoaderPackageDownloadService
{
    Task<string> DownloadAsync(
        MinecraftLoaderPackageEntry package,
        string destinationFile,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
