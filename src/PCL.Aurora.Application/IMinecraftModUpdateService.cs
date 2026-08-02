using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMinecraftModUpdateService
{
    Task<MinecraftModUpdateCheckResult> CheckAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        CancellationToken cancellationToken = default);

    Task<MinecraftModUpdateApplyResult> ApplyAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        IReadOnlyList<MinecraftModUpdateCandidate> updates,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
