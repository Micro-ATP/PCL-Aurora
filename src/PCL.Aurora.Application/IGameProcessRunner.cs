using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IGameProcessRunner
{
    Task<GameProcessSession> StartAsync(
        MinecraftGameLaunchRequest request,
        CancellationToken cancellationToken = default);
}
