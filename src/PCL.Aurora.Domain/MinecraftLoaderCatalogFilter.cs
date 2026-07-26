namespace PCL.Aurora.Domain;

public static class MinecraftLoaderCatalogFilter
{
    public static IReadOnlyList<MinecraftLoaderCatalogEntry> ForMinecraftVersion(
        MinecraftLoaderCatalog catalog,
        string minecraftVersion,
        MinecraftLoaderKind? kind = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftVersion);

        return catalog.Entries
            .Where(entry => string.Equals(entry.MinecraftVersion, minecraftVersion, StringComparison.OrdinalIgnoreCase))
            .Where(entry => kind is null || entry.Kind == kind)
            .OrderBy(entry => entry.Channel)
            .ThenByDescending(entry => entry.Version, new PclCeVersionComparer.VersionComparer())
            .ToArray();
    }
}
