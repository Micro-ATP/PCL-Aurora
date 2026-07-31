using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record MinecraftLaunchPatchPreparation(
    MinecraftGameLaunchRequest? Request,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => Request is not null && BlockingReasons.Count == 0;
}
