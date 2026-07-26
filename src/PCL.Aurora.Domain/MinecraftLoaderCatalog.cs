namespace PCL.Aurora.Domain;

public sealed record MinecraftLoaderCatalog(
    string SourceName,
    IReadOnlyList<MinecraftLoaderCatalogEntry> Entries);
