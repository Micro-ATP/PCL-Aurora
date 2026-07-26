namespace PCL.Aurora.Domain;

public sealed record MinecraftVersionCatalogParseResult(MinecraftVersionCatalog? Catalog, IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Catalog is not null && Errors.Count == 0;
}
