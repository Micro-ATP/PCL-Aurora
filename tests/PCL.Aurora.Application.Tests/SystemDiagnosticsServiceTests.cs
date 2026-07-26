using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class SystemDiagnosticsServiceTests
{
    [Fact]
    public async Task GetAsync_AggregatesPlatformPathsAndJavaInstallations()
    {
        var platform = new PlatformInformation("macOS", "15.7", JavaArchitecture.Arm64, ".NET 10");
        var paths = new PlatformPaths("/data", "/cache");
        var installations = new[]
        {
            new JavaInstallation("/java", "21.0.6", 21, "Test Vendor", JavaArchitecture.Arm64, JavaSource.Path, true),
        };
        var service = new SystemDiagnosticsService(
            new FakePlatformInfo(platform),
            new FakePlatformPaths(paths),
            new FakeJavaLocator(installations));

        var result = await service.GetAsync();

        Assert.Equal(platform, result.Platform);
        Assert.Equal(paths, result.Paths);
        Assert.Equal(installations, result.JavaInstallations);
    }

    private sealed class FakePlatformInfo(PlatformInformation information) : IPlatformInfo
    {
        public PlatformInformation Get() => information;
    }

    private sealed class FakePlatformPaths(PlatformPaths paths) : IPlatformPaths
    {
        public PlatformPaths Get() => paths;
    }

    private sealed class FakeJavaLocator(IReadOnlyList<JavaInstallation> installations) : IJavaLocator
    {
        public Task<IReadOnlyList<JavaInstallation>> FindAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(installations);
    }
}
