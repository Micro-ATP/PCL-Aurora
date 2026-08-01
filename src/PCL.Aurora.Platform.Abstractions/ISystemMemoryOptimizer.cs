namespace PCL.Aurora.Platform.Abstractions;

public interface ISystemMemoryOptimizer
{
    Task<SystemMemoryOptimizationResult> OptimizeAsync(CancellationToken cancellationToken = default);
}

public sealed record SystemMemoryOptimizationResult(
    bool Succeeded,
    long? AvailableBytesBefore,
    long? AvailableBytesAfter,
    long ManagedBytesReleased,
    bool UsedAdministratorPrivileges,
    string Detail)
{
    public long? SystemAvailableBytesGained =>
        AvailableBytesBefore is { } before && AvailableBytesAfter is { } after
            ? after - before
            : null;
}
