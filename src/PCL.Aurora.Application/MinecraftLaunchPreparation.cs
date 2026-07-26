using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record MinecraftLaunchPreparation(
    MinecraftVersionPreparation VersionPreparation,
    MinecraftLaunchArgumentPreparation ArgumentPreparation);
