namespace PCL.Aurora.Domain;

public sealed record MinecraftAssetMappingEntry(
    MinecraftAssetObject Asset,
    string SourcePath,
    string DestinationPath);
