using PCL.Aurora.Domain;

namespace PCL.Aurora.Platform.Abstractions;

public sealed record PlatformInformation(
    string OperatingSystem,
    string Version,
    JavaArchitecture Architecture,
    string RuntimeVersion);
