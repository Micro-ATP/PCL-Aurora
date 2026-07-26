using PCL.Aurora.Domain;

namespace PCL.Aurora.Platform.Abstractions;

public interface IJavaLocator
{
    Task<IReadOnlyList<JavaInstallation>> FindAllAsync(CancellationToken cancellationToken = default);
}
