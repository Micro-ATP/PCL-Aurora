using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed class MacOSMinecraftRootDirectoryProvider : IMinecraftRootDirectoryProvider
{
    public string GetRootDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Application Support",
        "minecraft");
}
