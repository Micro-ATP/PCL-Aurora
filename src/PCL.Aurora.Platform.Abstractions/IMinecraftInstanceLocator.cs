using PCL.Aurora.Domain;

namespace PCL.Aurora.Platform.Abstractions;

public interface IMinecraftInstanceLocator
{
    Task<IReadOnlyList<MinecraftInstance>> FindAllAsync(CancellationToken cancellationToken = default);
}
