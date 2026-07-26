using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

public sealed class SystemDiagnosticsService(
    IPlatformInfo platformInfo,
    IPlatformPaths platformPaths,
    IJavaLocator javaLocator) : ISystemDiagnosticsService
{
    public async Task<SystemDiagnostics> GetAsync(CancellationToken cancellationToken = default)
    {
        var javaInstallations = await javaLocator.FindAllAsync(cancellationToken).ConfigureAwait(false);
        return new SystemDiagnostics(platformInfo.Get(), platformPaths.Get(), javaInstallations);
    }
}
