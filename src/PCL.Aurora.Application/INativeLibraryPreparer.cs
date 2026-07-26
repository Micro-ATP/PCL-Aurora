using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface INativeLibraryPreparer
{
    Task<MinecraftNativeLibraryPreparation> PrepareAsync(
        MinecraftNativeLibraryPlan nativeLibraryPlan,
        CancellationToken cancellationToken = default);
}
