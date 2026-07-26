using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record MinecraftAssetPreparation(
    MinecraftAssetIndexParseResult IndexInspection,
    MinecraftAssetDownloadPlan DownloadPlan,
    MinecraftAssetMappingPlan MappingPlan);
