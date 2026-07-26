namespace PCL.Aurora.Domain;

public sealed record MinecraftVersionCatalogEntry(
    string Id,
    string Type,
    Uri MetadataUrl,
    DateTimeOffset ReleaseTime);
