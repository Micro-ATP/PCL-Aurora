using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class LaunchReadinessService : ILaunchReadinessService
{
    public LaunchReadiness Evaluate(MinecraftInstance? instance, MinecraftAccount? account, JavaInstallation? java) =>
        LaunchReadiness.Evaluate(instance, account, java);
}
