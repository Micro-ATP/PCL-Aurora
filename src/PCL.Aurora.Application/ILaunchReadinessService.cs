using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface ILaunchReadinessService
{
    LaunchReadiness Evaluate(MinecraftInstance? instance, MinecraftAccount? account, JavaInstallation? java);
}
