using PCL.Aurora.Platform.MacOS;

namespace PCL.Aurora.Platform.Tests;

public sealed class MacOSPlatformPathsTests
{
    [Fact]
    public void Get_ReturnsDistinctApplicationAndCacheDirectories()
    {
        var paths = new MacOSPlatformPaths().Get();

        Assert.NotEmpty(paths.ApplicationDataDirectory);
        Assert.NotEmpty(paths.CacheDirectory);
        Assert.NotEqual(paths.ApplicationDataDirectory, paths.CacheDirectory);
        Assert.Contains("PCL Aurora", paths.ApplicationDataDirectory, StringComparison.Ordinal);
        Assert.Contains("PCL Aurora", paths.CacheDirectory, StringComparison.Ordinal);
    }
}
