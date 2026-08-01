using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IJavaRuntimeInstallationService
{
    Task<JavaInstallation> InstallAsync(
        MinecraftJavaRequirement? requirement,
        IProgress<JavaRuntimeInstallationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
