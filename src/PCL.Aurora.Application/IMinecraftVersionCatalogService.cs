using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftVersionCatalogService
{
    Task<MinecraftVersionCatalogParseResult> FetchAsync(CancellationToken cancellationToken = default);
}
