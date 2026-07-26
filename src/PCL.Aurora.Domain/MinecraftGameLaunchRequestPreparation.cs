namespace PCL.Aurora.Domain;

public sealed record MinecraftGameLaunchRequestPreparation(
    MinecraftGameLaunchRequest? Request,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => Request is not null && BlockingReasons.Count == 0;
}
