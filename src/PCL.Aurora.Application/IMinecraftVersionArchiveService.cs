using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftVersionArchiveService
{
    Task<string> SaveClientCoreAsync(
        MinecraftVersionCatalogEntry version,
        string destinationDirectory,
        CancellationToken cancellationToken = default);

    Task SaveServerAsync(
        MinecraftVersionCatalogEntry version,
        string destinationFile,
        CancellationToken cancellationToken = default);
}
