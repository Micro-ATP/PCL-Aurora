namespace PCL.Aurora.Domain;

public sealed record MinecraftMemoryAllocationPreparation(
    MinecraftMemoryAllocation? Allocation,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => Allocation is not null && BlockingReasons.Count == 0;
}
