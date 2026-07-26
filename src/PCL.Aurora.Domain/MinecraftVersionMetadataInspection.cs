namespace PCL.Aurora.Domain;

public sealed record MinecraftVersionMetadataInspection(
    IReadOnlyList<MinecraftVersionMetadata> InheritanceChain,
    MinecraftVersionMetadata? EffectiveMetadata,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => EffectiveMetadata is not null && Errors.Count == 0;
}
