namespace PCL.Aurora.Domain;

public sealed record MinecraftVersionMetadata(
    string Id,
    string? InheritsFrom,
    string? Type,
    DateTimeOffset? ReleaseTime,
    MinecraftVersionDownload? ClientDownload,
    MinecraftVersionAssetIndex? AssetIndex);
