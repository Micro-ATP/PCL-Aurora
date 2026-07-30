namespace PCL.Aurora.Domain;

// Adapted from PCL-CE McInstance.PathIndie. Aurora resolves the game directory
// per launch instead of storing a separate per-instance migration flag.
public static class MinecraftInstanceIsolationResolver
{
    public static string ResolveGameDirectory(
        MinecraftInstance instance,
        string minecraftRootDirectory,
        MinecraftInstanceIsolationMode mode)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftRootDirectory);

        if (HasInstanceData(instance.DirectoryPath))
        {
            return instance.DirectoryPath;
        }

        var isRelease = string.Equals(instance.Type, "release", StringComparison.OrdinalIgnoreCase);
        var isModded = instance.InstalledLoader is not null || instance.HasOptiFine;
        var shouldIsolate = mode switch
        {
            MinecraftInstanceIsolationMode.Disabled => false,
            MinecraftInstanceIsolationMode.ModdedOnly => isModded,
            MinecraftInstanceIsolationMode.NonReleaseOnly => !isRelease,
            MinecraftInstanceIsolationMode.ModdedAndNonRelease => isModded || !isRelease,
            MinecraftInstanceIsolationMode.All => true,
            _ => true,
        };
        return shouldIsolate ? instance.DirectoryPath : minecraftRootDirectory;
    }

    private static bool HasInstanceData(string instanceDirectory)
    {
        try
        {
            var modsDirectory = Path.Combine(instanceDirectory, "mods");
            if (Directory.Exists(modsDirectory) && Directory.EnumerateFiles(modsDirectory).Any())
            {
                return true;
            }

            var savesDirectory = Path.Combine(instanceDirectory, "saves");
            return Directory.Exists(savesDirectory) && Directory.EnumerateDirectories(savesDirectory).Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
