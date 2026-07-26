namespace PCL.Aurora.Domain;

public sealed record MinecraftAssetIndex(
    string Id,
    IReadOnlyList<MinecraftAssetObject> Objects,
    bool IsVirtual,
    bool MapsToResources);
