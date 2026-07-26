namespace PCL.Aurora.Domain;

public sealed record MinecraftLaunchArgumentPreparation(
    MinecraftLaunchArguments? Arguments,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => Arguments is not null && BlockingReasons.Count == 0;
}
