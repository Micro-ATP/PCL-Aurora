using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record MinecraftVersionPreparation(
    MinecraftVersionMetadataInspection Inspection,
    MinecraftDownloadPlan DownloadPlan);
