using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IInstanceCatalogService
{
    Task<IReadOnlyList<MinecraftInstance>> GetAllAsync(CancellationToken cancellationToken = default);
}
