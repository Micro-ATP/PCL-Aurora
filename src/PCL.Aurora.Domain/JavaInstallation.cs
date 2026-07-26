namespace PCL.Aurora.Domain;

public sealed record JavaInstallation(
    string ExecutablePath,
    string? Version,
    int? MajorVersion,
    string Vendor,
    JavaArchitecture Architecture,
    JavaSource Source,
    bool IsCompatible);
