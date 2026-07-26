using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record MinecraftGameLaunchPreparation(
    LaunchReadiness Readiness,
    MinecraftLaunchPreparation? LaunchPreparation,
    MinecraftGameLaunchRequestPreparation RequestPreparation,
    IReadOnlyList<string> BlockingReasons)
{
    public bool CanLaunch => RequestPreparation.IsReady && BlockingReasons.Count == 0;
}
