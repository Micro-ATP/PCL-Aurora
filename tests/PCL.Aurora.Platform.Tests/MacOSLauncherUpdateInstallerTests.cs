using System.Runtime.InteropServices;
using PCL.Aurora.Platform.Abstractions;
using PCL.Aurora.Platform.MacOS;

namespace PCL.Aurora.Platform.Tests;

public sealed class MacOSLauncherUpdateInstallerTests
{
    [Fact]
    public void SelectPackage_SelectsCurrentArchitectureAndChecksum()
    {
        var installer = new MacOSLauncherUpdateInstaller(new HttpClient(), new FixedPlatformPaths());
        var expectedArchitecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var assets = new[]
        {
            Asset("PCL-Aurora-1.4.0-osx-arm64.zip"),
            Asset("PCL-Aurora-1.4.0-osx-x64.zip"),
            Asset("SHA256SUMS"),
        };

        var package = installer.SelectPackage(assets);

        Assert.Contains($"osx-{expectedArchitecture}", package.Archive.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("SHA256SUMS", package.Checksum.Name);
    }

    [Fact]
    public void SelectPackage_RejectsReleaseWithoutChecksum()
    {
        var installer = new MacOSLauncherUpdateInstaller(new HttpClient(), new FixedPlatformPaths());
        var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";

        var exception = Assert.Throws<InvalidDataException>(() =>
            installer.SelectPackage([Asset($"PCL-Aurora-1.4.0-osx-{architecture}.zip")]));

        Assert.Contains("SHA-256", exception.Message, StringComparison.Ordinal);
    }

    private static LauncherUpdateAsset Asset(string name) => new(
        name,
        new Uri($"https://github.com/Micro-ATP/PCL-Aurora/releases/download/v1.4.0/{name}"),
        1024,
        null);

    private sealed class FixedPlatformPaths : IPlatformPaths
    {
        public PlatformPaths Get()
        {
            var root = Path.Combine(Path.GetTempPath(), "pcl-aurora-tests");
            return new(root, Path.Combine(root, "Cache"));
        }
    }
}
