using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftGameLaunchService
{
    Task<MinecraftGameLaunchPreparation> PrepareAsync(
        MinecraftInstance? instance,
        MinecraftAccount? account,
        JavaInstallation? java,
        CancellationToken cancellationToken = default);

    Task<GameProcessSession> LaunchAsync(
        MinecraftGameLaunchPreparation preparation,
        CancellationToken cancellationToken = default);
}
