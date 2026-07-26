using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

public sealed record SystemDiagnostics(
    PlatformInformation Platform,
    PlatformPaths Paths,
    IReadOnlyList<JavaInstallation> JavaInstallations);
