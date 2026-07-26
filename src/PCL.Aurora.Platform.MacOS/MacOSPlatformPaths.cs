using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed class MacOSPlatformPaths : IPlatformPaths
{
    public PlatformPaths Get()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var library = Path.Combine(home, "Library");

        return new PlatformPaths(
            ApplicationDataDirectory: Path.Combine(library, "Application Support", "PCL Aurora"),
            CacheDirectory: Path.Combine(library, "Caches", "PCL Aurora"));
    }
}
