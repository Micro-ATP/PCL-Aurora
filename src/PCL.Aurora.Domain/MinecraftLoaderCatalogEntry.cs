namespace PCL.Aurora.Domain;

public sealed record MinecraftLoaderCatalogEntry(
    MinecraftLoaderKind Kind,
    string MinecraftVersion,
    string Version,
    MinecraftLoaderChannel Channel,
    bool IsRecommended,
    PclCeForgelikeEntry? ForgelikeEntry,
    PclCeOptiFineVersionEntry? OptiFineEntry = null)
{
    public bool IsPrerelease => Channel is not MinecraftLoaderChannel.Release;
}
