namespace PCL.Aurora.Domain;

public sealed record MinecraftMemoryAllocation(
    int MaximumMemoryMiB,
    bool IsAutomatic,
    bool IsLimitedFor32BitJava);
