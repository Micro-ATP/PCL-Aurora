namespace PCL.Aurora.Domain;

public sealed record MinecraftLoaderCatalogParseResult(
    MinecraftLoaderCatalog? Catalog,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Catalog is not null && Errors.Count == 0;
}
