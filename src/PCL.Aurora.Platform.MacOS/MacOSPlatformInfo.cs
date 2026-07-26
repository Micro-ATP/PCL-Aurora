using System.Runtime.InteropServices;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed class MacOSPlatformInfo : IPlatformInfo
{
    public PlatformInformation Get()
    {
        return new PlatformInformation(
            OperatingSystem: "macOS",
            Version: Environment.OSVersion.VersionString,
            Architecture: ToJavaArchitecture(RuntimeInformation.OSArchitecture),
            RuntimeVersion: RuntimeInformation.FrameworkDescription);
    }

    private static JavaArchitecture ToJavaArchitecture(Architecture architecture) => architecture switch
    {
        Architecture.Arm64 => JavaArchitecture.Arm64,
        Architecture.X64 => JavaArchitecture.X64,
        _ => JavaArchitecture.Unknown,
    };
}
