namespace PCL.Aurora.Domain;

public sealed record MinecraftVersionCatalog(
    string? LatestRelease,
    string? LatestSnapshot,
    IReadOnlyList<MinecraftVersionCatalogEntry> Versions);
