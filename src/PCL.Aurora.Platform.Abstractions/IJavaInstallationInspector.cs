using PCL.Aurora.Domain;

namespace PCL.Aurora.Platform.Abstractions;

public interface IJavaInstallationInspector
{
    Task<JavaInstallation?> InspectAsync(
        string executablePath,
        CancellationToken cancellationToken = default);
}
